using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Constraints;

public record ConstraintDto(
    Guid Id, Guid TeacherId, string TeacherName,
    string ConstraintType, int DayOfWeek, int SlotIndex,
    int Weight, string? Reason);

public record CreateConstraintRequest(
    Guid TeacherId, string ConstraintType,
    int DayOfWeek, int SlotIndex,
    int Weight, string? Reason);

public static class ConstraintEndpoints
{
    public static IEndpointRouteBuilder MapConstraintEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/constraints");

        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var constraints = await db.TeacherConstraints.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId)
                .ToListAsync();
            var teacherNames = await db.Teachers.AsNoTracking()
                .Where(t => t.SchoolId == user.SchoolId)
                .ToDictionaryAsync(t => t.Id, t => t.FullName);
            return Results.Ok(constraints.Select(c => ToDto(c, teacherNames)));
        });

        g.MapGet("/teacher/{teacherId:guid}", async (Guid teacherId, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var constraints = await db.TeacherConstraints.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId && c.TeacherId == teacherId)
                .ToListAsync();
            var name = await db.Teachers.AsNoTracking()
                .Where(t => t.Id == teacherId && t.SchoolId == user.SchoolId)
                .Select(t => t.FullName).FirstOrDefaultAsync();
            var nameMap = name is not null ? new Dictionary<Guid, string> { [teacherId] = name } : new();
            return Results.Ok(constraints.Select(c => ToDto(c, nameMap)));
        });

        g.MapPost("/", async (HttpContext ctx, AppDbContext db, CreateConstraintRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.Forbid();
            var c = new TeacherConstraint
            {
                SchoolId = user.SchoolId, TeacherId = req.TeacherId,
                ConstraintType = req.ConstraintType, DayOfWeek = req.DayOfWeek,
                SlotIndex = req.SlotIndex, Weight = req.Weight, Reason = req.Reason,
            };
            db.TeacherConstraints.Add(c);
            await db.SaveChangesAsync();
            var name = await db.Teachers.AsNoTracking()
                .Where(t => t.Id == req.TeacherId).Select(t => t.FullName).FirstOrDefaultAsync() ?? "?";
            return Results.Created($"/api/constraints/{c.Id}", ToDto(c, new() { [req.TeacherId] = name }));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.Forbid();
            var c = await db.TeacherConstraints.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (c is null) return Results.NotFound();
            db.TeacherConstraints.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static ConstraintDto ToDto(TeacherConstraint c, Dictionary<Guid, string> names)
        => new(c.Id, c.TeacherId, names.GetValueOrDefault(c.TeacherId, "?"),
            c.ConstraintType, c.DayOfWeek, c.SlotIndex, c.Weight, c.Reason);
}
