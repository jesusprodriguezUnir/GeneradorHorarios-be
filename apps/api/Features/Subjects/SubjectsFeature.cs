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
            var templates = await db.CurriculumTemplates.AsNoTracking()
                .Where(t => t.IsOfficial || t.SchoolId == user.SchoolId)
                .ToListAsync();

            var templateIds = templates.Select(t => t.Id).ToList();
            var allocations = await db.SubjectAllocations.AsNoTracking()
                .Where(a => templateIds.Contains(a.TemplateId))
                .ToListAsync();

            return Results.Ok(allocations.Select(a => new SubjectDto(
                a.Id, a.SubjectName, a.SubjectShort, a.SubjectKey,
                a.WeeklyHoursMin, a.WeeklyHoursMax, a.WeeklyHoursDefault,
                a.RequiresSpecialist, a.RequiredClassroomType,
                a.MaxConsecutiveSlots, a.SplittableAcrossDays, a.IsOfficial)));
        });

        // PUT /api/subjects/{id}/hours — ajustar horas de una asignatura (dentro del rango)
        g.MapPut("/{id:guid}/hours", async (Guid id, HttpContext ctx, AppDbContext db, UpdateSubjectHoursRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.Forbid();

            var a = await db.SubjectAllocations.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();

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

        return app;
    }
}
