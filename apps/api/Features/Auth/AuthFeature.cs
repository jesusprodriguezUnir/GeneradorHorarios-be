using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        // GET /api/auth/me — datos del usuario autenticado
        g.MapGet("/me", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.Items["CurrentUser"] as ICurrentUser;
            if (user is null) return Results.Unauthorized();

            var teacher = user.IsTeacher
                ? await db.Teachers.AsNoTracking()
                    .Where(t => t.UserId == user.UserId)
                    .Select(t => new { t.Id, t.FullName, t.ColorKey })
                    .FirstOrDefaultAsync()
                : null;

            var school = await db.Schools.AsNoTracking()
                .Where(s => s.Id == user.SchoolId)
                .Select(s => new { s.Id, s.Name, s.Slug })
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                userId   = user.UserId,
                schoolId = user.SchoolId,
                role     = user.Role,
                school,
                teacher,
            });
        });

        // GET /api/auth/demo-users — lista de usuarios demo para el selector de login
        g.MapGet("/demo-users", async (AppDbContext db) =>
        {
            var users = await db.AppUsers.AsNoTracking()
                .Select(u => new { u.Id, u.Email, u.FullName, u.Role })
                .ToListAsync();
            return Results.Ok(users);
        });

        return app;
    }
}
