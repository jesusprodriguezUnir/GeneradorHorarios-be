using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Assignments;

public record AssignmentDto(
    Guid Id, Guid TeacherId, string TeacherName,
    Guid GroupId, string GroupDisplay,
    Guid AllocationId, string SubjectName,
    int WeeklyHours);

public record AssignmentSummaryDto(
    string SubjectName, string SubjectKey,
    int RequiredHours, int AssignedHours,
    double CompletionPct,
    IReadOnlyList<AssignmentDto> Assignments);

public record CreateAssignmentRequest(Guid TeacherId, Guid GroupId, Guid AllocationId, int WeeklyHours);

public static class AssignmentEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/assignments");

        // GET /api/assignments — con resumen de completitud por asignatura
        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();

            var assignments = await db.Assignments.AsNoTracking()
                .Where(a => a.SchoolId == user.SchoolId)
                .ToListAsync();

            var teacherNames = await db.Teachers.AsNoTracking()
                .Where(t => t.SchoolId == user.SchoolId)
                .ToDictionaryAsync(t => t.Id, t => t.FullName);

            var groupNames = await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId)
                .ToDictionaryAsync(g => g.Id, g => g.DisplayName);

            var allocations = await db.SubjectAllocations.AsNoTracking()
                .ToDictionaryAsync(a => a.Id);

            // Agrupar por asignatura para calcular completitud
            var courseGroups = await db.CourseGroups.AsNoTracking()
                .Include(x => x.SubjectHoursList)
                .Where(g => g.SchoolId == user.SchoolId)
                .ToListAsync();

            var summary = assignments
                .GroupBy(a => a.AllocationId)
                .Select(grp =>
                {
                    var alloc = allocations.GetValueOrDefault(grp.Key);
                    var required = 0;
                    if (alloc != null)
                    {
                        foreach (var cg in courseGroups)
                        {
                            var groupHours = cg.SubjectHoursList?.ToDictionary(x => x.SubjectKey, x => x.Hours) ?? new();
                            if (groupHours.TryGetValue(alloc.SubjectKey, out var h))
                            {
                                required += h;
                            }
                            else
                            {
                                required += alloc.WeeklyHoursDefault;
                            }
                        }
                    }
                    var assigned = grp.Sum(a => a.WeeklyHours);
                    return new AssignmentSummaryDto(
                        alloc?.SubjectName ?? "?",
                        alloc?.SubjectKey ?? "tut",
                        required, assigned,
                        required == 0 ? 100 : Math.Min(100.0, assigned * 100.0 / required),
                        grp.Select(a => new AssignmentDto(
                            a.Id,
                            a.TeacherId, teacherNames.GetValueOrDefault(a.TeacherId, "?"),
                            a.GroupId,   groupNames.GetValueOrDefault(a.GroupId, "?"),
                            a.AllocationId, alloc?.SubjectName ?? "?",
                            a.WeeklyHours
                        )).ToList()
                    );
                })
                .OrderBy(s => s.SubjectName)
                .ToList();

            return Results.Ok(summary);
        });

        // POST /api/assignments
        g.MapPost("/", async (HttpContext ctx, AppDbContext db, CreateAssignmentRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.Forbid();

            // Verificar que teacher y group pertenecen al colegio
            var teacherOk = await db.Teachers.AnyAsync(t => t.Id == req.TeacherId && t.SchoolId == user.SchoolId);
            var groupOk = await db.CourseGroups.AnyAsync(g => g.Id == req.GroupId && g.SchoolId == user.SchoolId);
            if (!teacherOk || !groupOk) return Results.BadRequest(new { message = "Profesor o grupo no válido." });

            var a = new Assignment
            {
                SchoolId = user.SchoolId, TeacherId = req.TeacherId,
                GroupId = req.GroupId, AllocationId = req.AllocationId,
                WeeklyHours = req.WeeklyHours,
            };
            db.Assignments.Add(a);
            await db.SaveChangesAsync();
            return Results.Created($"/api/assignments/{a.Id}", a);
        });

        // DELETE /api/assignments/{id}
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.Forbid();
            var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (a is null) return Results.NotFound();
            db.Assignments.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
