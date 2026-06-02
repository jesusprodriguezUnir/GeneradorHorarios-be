using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Subjects;

public record SubjectDto(
    Guid Id, string SubjectName, string SubjectShort, string SubjectKey,
    int WeeklyHoursMin, int WeeklyHoursMax, int WeeklyHoursDefault,
    bool RequiresSpecialist, string? RequiredClassroomType,
    int MaxConsecutiveSlots, bool SplittableAcrossDays, bool IsOfficial);

public record UpdateSubjectHoursRequest(int WeeklyHoursDefault);

public record CreateSubjectRequest(
    string SubjectName, string SubjectShort, string SubjectKey,
    int WeeklyHoursMin, int WeeklyHoursMax, int WeeklyHoursDefault,
    bool RequiresSpecialist, string? RequiredClassroomType,
    int MaxConsecutiveSlots, bool SplittableAcrossDays);

public record UpdateSubjectRequest(
    string? SubjectName, string? SubjectShort, string? SubjectKey,
    int? WeeklyHoursMin, int? WeeklyHoursMax, int? WeeklyHoursDefault,
    bool? RequiresSpecialist, string? RequiredClassroomType,
    int? MaxConsecutiveSlots, bool? SplittableAcrossDays);

public static class SubjectEndpoints
{
    public static IEndpointRouteBuilder MapSubjectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/subjects");

        // GET /api/subjects — plantilla oficial + personalizaciones del colegio
        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();

            // Buscar template del colegio o la oficial
            var customTemplate = await db.CurriculumTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchoolId == user.SchoolId);

            var templateId = customTemplate?.Id;
            if (templateId == null)
            {
                var officialTemplate = await db.CurriculumTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.IsOfficial);
                templateId = officialTemplate?.Id;
            }

            if (templateId == null)
                return Results.Ok(Enumerable.Empty<SubjectDto>());

            var allocations = await db.SubjectAllocations.AsNoTracking()
                .Where(a => a.TemplateId == templateId)
                .ToListAsync();

            return Results.Ok(allocations.Select(a => new SubjectDto(
                a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
                a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
                a.RequiresSpecialist, a.RequiredClassroomType,
                a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial)));
        });

        // POST /api/subjects/clone-official — clonar plantilla oficial para el colegio
        g.MapPost("/clone-official", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            // Verificar si el colegio ya tiene una plantilla propia
            var exists = await db.CurriculumTemplates.AsNoTracking()
                .AnyAsync(t => t.SchoolId == user.SchoolId);
            if (exists)
                return Results.BadRequest(new { message = "El colegio ya tiene una plantilla configurada." });

            // Buscar la plantilla oficial
            var officialTemplate = await db.CurriculumTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsOfficial);
            if (officialTemplate is null)
                return Results.NotFound(new { message = "Plantilla LOMLOE oficial no encontrada." });

            // Obtener el nombre del colegio
            var schoolName = await db.Schools.Where(s => s.Id == user.SchoolId)
                .Select(s => s.Name).FirstOrDefaultAsync() ?? "Colegio";

            // Crear la nueva plantilla personalizada del centro
            var newTemplate = new CurriculumTemplate
            {
                SchoolId = user.SchoolId,
                Name = $"Plantilla de {schoolName}",
                Region = officialTemplate.Region,
                Stage = officialTemplate.Stage,
                IsOfficial = false
            };
            db.CurriculumTemplates.Add(newTemplate);

            // Obtener las asignaturas oficiales
            var officialAllocations = await db.SubjectAllocations.AsNoTracking()
                .Where(a => a.TemplateId == officialTemplate.Id)
                .ToListAsync();

            // Clonar las asignaturas oficiales a la nueva plantilla
            var clonedAllocations = officialAllocations.Select(a => new SubjectAllocation
            {
                TemplateId = newTemplate.Id,
                SubjectName = a.SubjectName,
                SubjectShort = a.SubjectShort,
                SubjectKey = a.SubjectKey,
                WeeklyHoursMin = a.WeeklyHoursMin,
                WeeklyHoursMax = a.WeeklyHoursMax,
                WeeklyHoursDefault = a.WeeklyHoursDefault,
                RequiresSpecialist = a.RequiresSpecialist,
                RequiredClassroomType = a.RequiredClassroomType,
                MaxConsecutiveSlots = a.MaxConsecutiveSlots,
                SplittableAcrossDays = a.SplittableAcrossDays,
                IsOfficial = false
            }).ToList();

            db.SubjectAllocations.AddRange(clonedAllocations);
            await db.SaveChangesAsync();

            return Results.Ok(clonedAllocations.Select(a => new SubjectDto(
                a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
                a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
                a.RequiresSpecialist, a.RequiredClassroomType,
                a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial)));
        });

        // PUT /api/subjects/{id}/hours — ajustar horas de una asignatura (dentro del rango)
        g.MapPut("/{id:guid}/hours", async (Guid id, HttpContext ctx, AppDbContext db, UpdateSubjectHoursRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var a = await db.SubjectAllocations.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();

            // Validar fuga cross-tenant y edición de plantilla oficial
            var template = await db.CurriculumTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == a.TemplateId);
            if (template is null)
                return Results.BadRequest(new { message = "La plantilla asociada no existe." });

            if (template.IsOfficial)
                return Results.BadRequest(new { message = "No se puede modificar la plantilla LOMLOE oficial. Primero debes clonarla para tu centro." });

            if (template.SchoolId != user.SchoolId)
                return Results.StatusCode(403);

            if (req.WeeklyHoursDefault < a.WeeklyHoursMin || req.WeeklyHoursDefault > a.WeeklyHoursMax)
                return Results.BadRequest(new
                {
                    message = $"Las horas deben estar entre {a.WeeklyHoursMin} y {a.WeeklyHoursMax} según la normativa LOMLOE Madrid."
                });

            a.WeeklyHoursDefault = req.WeeklyHoursDefault;
            await db.SaveChangesAsync();
            return Results.Ok(new SubjectDto(a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
                a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
                a.RequiresSpecialist, a.RequiredClassroomType,
                a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial));
        });

        // POST /api/subjects — crear una nueva asignatura en el currículo personalizado
        g.MapPost("/", async (HttpContext ctx, AppDbContext db, CreateSubjectRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            // Buscar la plantilla personalizada del centro
            var customTemplate = await db.CurriculumTemplates
                .FirstOrDefaultAsync(t => t.SchoolId == user.SchoolId && !t.IsOfficial);
            if (customTemplate is null)
                return Results.BadRequest(new { message = "Debes personalizar (clonar) el currículo LOMLOE antes de añadir asignaturas." });

            var s = new SubjectAllocation
            {
                TemplateId = customTemplate.Id,
                SubjectName = req.SubjectName,
                SubjectShort = req.SubjectShort,
                SubjectKey = req.SubjectKey.ToLower().Trim(),
                WeeklyHoursMin = req.WeeklyHoursMin,
                WeeklyHoursMax = req.WeeklyHoursMax,
                WeeklyHoursDefault = req.WeeklyHoursDefault,
                RequiresSpecialist = req.RequiresSpecialist,
                RequiredClassroomType = req.RequiredClassroomType,
                MaxConsecutiveSlots = req.MaxConsecutiveSlots,
                SplittableAcrossDays = req.SplittableAcrossDays,
                IsOfficial = false
            };

            db.SubjectAllocations.Add(s);
            await db.SaveChangesAsync();

            return Results.Created($"/api/subjects/{s.Id}", new SubjectDto(
                s.Id, s.SubjectName, s.SubjectShort, s.SubjectKey,
                s.WeeklyHoursMin, s.WeeklyHoursMax, s.WeeklyHoursDefault,
                s.RequiresSpecialist, s.RequiredClassroomType,
                s.MaxConsecutiveSlots, s.SplittableAcrossDays, s.IsOfficial));
        });

        // PUT /api/subjects/{id} — editar al completo una asignatura personalizada
        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, UpdateSubjectRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var a = await db.SubjectAllocations.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();

            var template = await db.CurriculumTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == a.TemplateId);
            if (template is null || template.IsOfficial || template.SchoolId != user.SchoolId)
                return Results.StatusCode(403);

            if (req.SubjectName is not null) a.SubjectName = req.SubjectName;
            if (req.SubjectShort is not null) a.SubjectShort = req.SubjectShort;
            if (req.SubjectKey is not null) a.SubjectKey = req.SubjectKey.ToLower().Trim();
            if (req.WeeklyHoursMin.HasValue) a.WeeklyHoursMin = req.WeeklyHoursMin.Value;
            if (req.WeeklyHoursMax.HasValue) a.WeeklyHoursMax = req.WeeklyHoursMax.Value;
            if (req.WeeklyHoursDefault.HasValue) a.WeeklyHoursDefault = req.WeeklyHoursDefault.Value;
            if (req.RequiresSpecialist.HasValue) a.RequiresSpecialist = req.RequiresSpecialist.Value;
            a.RequiredClassroomType = req.RequiredClassroomType; // puede ser nulo
            if (req.MaxConsecutiveSlots.HasValue) a.MaxConsecutiveSlots = req.MaxConsecutiveSlots.Value;
            if (req.SplittableAcrossDays.HasValue) a.SplittableAcrossDays = req.SplittableAcrossDays.Value;

            await db.SaveChangesAsync();

            return Results.Ok(new SubjectDto(
                a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
                a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
                a.RequiresSpecialist, a.RequiredClassroomType,
                a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial));
        });

        // DELETE /api/subjects/{id} — eliminar una asignatura personalizada
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var a = await db.SubjectAllocations.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();

            var template = await db.CurriculumTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == a.TemplateId);
            if (template is null || template.IsOfficial || template.SchoolId != user.SchoolId)
                return Results.StatusCode(403);

            db.SubjectAllocations.Remove(a);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return app;
    }
}
