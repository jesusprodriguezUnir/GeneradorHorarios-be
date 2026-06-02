using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Groups;

public record GroupDto(Guid Id, int CourseLevel, string GroupLabel, string DisplayName,
    int StudentCount, Guid? TutorId, string? TutorName, Guid? HomeClassroomId, Dictionary<string, int> SubjectHours);

public record CreateGroupRequest(int CourseLevel, string GroupLabel, int StudentCount, Guid? TutorId, Guid? HomeClassroomId, Dictionary<string, int>? SubjectHours);
public record UpdateGroupRequest(int? CourseLevel, string? GroupLabel, int? StudentCount, Guid? TutorId, Guid? HomeClassroomId, Dictionary<string, int>? SubjectHours);

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/groups");

        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var groups = await db.CourseGroups.AsNoTracking()
                .Include(x => x.SubjectHoursList)
                .Where(x => x.SchoolId == user.SchoolId)
                .OrderBy(x => x.CourseLevel).ThenBy(x => x.GroupLabel)
                .ToListAsync();
            var tutorNames = await db.Teachers.AsNoTracking()
                .Where(t => t.SchoolId == user.SchoolId)
                .ToDictionaryAsync(t => t.Id, t => t.FullName);
            return Results.Ok(groups.Select(gr => ToDto(gr, tutorNames)));
        });

        g.MapPost("/", async (HttpContext ctx, AppDbContext db, CreateGroupRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var gr = new CourseGroup
            {
                SchoolId = user.SchoolId, CourseLevel = req.CourseLevel, GroupLabel = req.GroupLabel,
                StudentCount = req.StudentCount, TutorId = req.TutorId, HomeClassroomId = req.HomeClassroomId,
            };
            if (req.SubjectHours is not null)
            {
                gr.SubjectHoursList = req.SubjectHours.Select(kv => new GroupSubjectHour
                {
                    GroupId = gr.Id,
                    SubjectKey = kv.Key.ToLower().Trim(),
                    Hours = kv.Value
                }).ToList();
            }
            db.CourseGroups.Add(gr);
            await db.SaveChangesAsync();
            return Results.Created($"/api/groups/{gr.Id}", ToDto(gr, []));
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, UpdateGroupRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var gr = await db.CourseGroups
                .Include(x => x.SubjectHoursList)
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (gr is null) return Results.NotFound();
            if (req.CourseLevel.HasValue) gr.CourseLevel = req.CourseLevel.Value;
            if (req.GroupLabel is not null) gr.GroupLabel = req.GroupLabel;
            if (req.StudentCount.HasValue) gr.StudentCount = req.StudentCount.Value;
            gr.TutorId = req.TutorId;
            gr.HomeClassroomId = req.HomeClassroomId;
            if (req.SubjectHours is not null)
            {
                // Limpiar horas anteriores
                db.GroupSubjectHours.RemoveRange(gr.SubjectHoursList);
                // Guardar las nuevas
                gr.SubjectHoursList = req.SubjectHours.Select(kv => new GroupSubjectHour
                {
                    GroupId = gr.Id,
                    SubjectKey = kv.Key.ToLower().Trim(),
                    Hours = kv.Value
                }).ToList();
            }
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(gr, []));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var gr = await db.CourseGroups.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (gr is null) return Results.NotFound();
            db.CourseGroups.Remove(gr);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static GroupDto ToDto(CourseGroup gr, Dictionary<Guid, string> tutorNames)
    {
        var subjectHours = gr.SubjectHoursList?.ToDictionary(x => x.SubjectKey, x => x.Hours) ?? new();
        return new(gr.Id, gr.CourseLevel, gr.GroupLabel, gr.DisplayName, gr.StudentCount,
            gr.TutorId, gr.TutorId.HasValue && tutorNames.TryGetValue(gr.TutorId.Value, out var n) ? n : null,
            gr.HomeClassroomId, subjectHours);
    }
}
