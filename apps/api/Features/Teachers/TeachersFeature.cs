using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Teachers;

public record TeacherDto(
    Guid Id, string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, string[] Specialties, string ColorKey,
    int AssignedHours);

public record CreateTeacherRequest(
    string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, string[] Specialties, string ColorKey);

public record UpdateTeacherRequest(
    string? FullName, string? Email, string? TeacherType,
    int? MaxWeeklyHours, string[]? Specialties, string? ColorKey);

public static class TeacherEndpoints
{
    public static IEndpointRouteBuilder MapTeacherEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/teachers");

        // GET /api/teachers
        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var teachers = await db.Teachers.AsNoTracking()
                .Where(t => t.SchoolId == user.SchoolId)
                .ToListAsync();

            // Horas asignadas por profesor
            var assignedHours = await db.Assignments.AsNoTracking()
                .Where(a => a.SchoolId == user.SchoolId)
                .GroupBy(a => a.TeacherId)
                .Select(g => new { TeacherId = g.Key, Hours = g.Sum(a => a.WeeklyHours) })
                .ToListAsync();
            var hoursMap = assignedHours.ToDictionary(x => x.TeacherId, x => x.Hours);

            var result = teachers.Select(t => ToDto(t, hoursMap.GetValueOrDefault(t.Id)));
            return Results.Ok(result);
        });

        // GET /api/teachers/{id}
        g.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var t = await db.Teachers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (t is null) return Results.NotFound();
            var hours = await db.Assignments.AsNoTracking()
                .Where(a => a.TeacherId == id).SumAsync(a => a.WeeklyHours);
            return Results.Ok(ToDto(t, hours));
        });

        // POST /api/teachers
        g.MapPost("/", async (HttpContext ctx, AppDbContext db, CreateTeacherRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var t = new Teacher
            {
                SchoolId = user.SchoolId, FullName = req.FullName, Email = req.Email,
                TeacherType = req.TeacherType, MaxWeeklyHours = req.MaxWeeklyHours,
                Specialties = System.Text.Json.JsonSerializer.Serialize(req.Specialties),
                ColorKey = req.ColorKey,
            };
            db.Teachers.Add(t);
            await db.SaveChangesAsync();
            return Results.Created($"/api/teachers/{t.Id}", ToDto(t, 0));
        });

        // PUT /api/teachers/{id}
        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, UpdateTeacherRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var t = await db.Teachers.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (t is null) return Results.NotFound();
            if (req.FullName is not null) t.FullName = req.FullName;
            if (req.Email is not null) t.Email = req.Email;
            if (req.TeacherType is not null) t.TeacherType = req.TeacherType;
            if (req.MaxWeeklyHours.HasValue) t.MaxWeeklyHours = req.MaxWeeklyHours.Value;
            if (req.Specialties is not null) t.Specialties = System.Text.Json.JsonSerializer.Serialize(req.Specialties);
            if (req.ColorKey is not null) t.ColorKey = req.ColorKey;
            await db.SaveChangesAsync();
            var hours = await db.Assignments.AsNoTracking().Where(a => a.TeacherId == id).SumAsync(a => a.WeeklyHours);
            return Results.Ok(ToDto(t, hours));
        });

        // DELETE /api/teachers/{id}
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var t = await db.Teachers.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (t is null) return Results.NotFound();
            db.Teachers.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static TeacherDto ToDto(Teacher t, int assignedHours)
    {
        string[] specialties;
        try { specialties = System.Text.Json.JsonSerializer.Deserialize<string[]>(t.Specialties) ?? []; }
        catch { specialties = []; }
        return new(t.Id, t.FullName, t.Email, t.TeacherType,
            t.MaxWeeklyHours, specialties, t.ColorKey, assignedHours);
    }
}
