using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Classrooms;

public record ClassroomDto(Guid Id, string Name, string ClassroomType, int Capacity, bool IsShared);
public record CreateClassroomRequest(string Name, string ClassroomType, int Capacity, bool IsShared);
public record UpdateClassroomRequest(string? Name, string? ClassroomType, int? Capacity, bool? IsShared);

public static class ClassroomEndpoints
{
    public static IEndpointRouteBuilder MapClassroomEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/classrooms");

        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var list = await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId)
                .OrderBy(c => c.Name)
                .Select(c => new ClassroomDto(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared))
                .ToListAsync();
            return Results.Ok(list);
        });

        g.MapPost("/", async (HttpContext ctx, AppDbContext db, CreateClassroomRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var c = new Classroom
            {
                SchoolId = user.SchoolId, Name = req.Name, ClassroomType = req.ClassroomType,
                Capacity = req.Capacity, IsShared = req.IsShared,
            };
            db.Classrooms.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/classrooms/{c.Id}", new ClassroomDto(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared));
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db, UpdateClassroomRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var c = await db.Classrooms.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (c is null) return Results.NotFound();
            if (req.Name is not null) c.Name = req.Name;
            if (req.ClassroomType is not null) c.ClassroomType = req.ClassroomType;
            if (req.Capacity.HasValue) c.Capacity = req.Capacity.Value;
            if (req.IsShared.HasValue) c.IsShared = req.IsShared.Value;
            await db.SaveChangesAsync();
            return Results.Ok(new ClassroomDto(c.Id, c.Name, c.ClassroomType, c.Capacity, c.IsShared));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            var c = await db.Classrooms.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == user.SchoolId);
            if (c is null) return Results.NotFound();
            db.Classrooms.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
