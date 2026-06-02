using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Features.Auth;

/// <summary>
/// Middleware de autenticación simulada para desarrollo.
/// Lee el header X-User-Email (o X-User-Id) y resuelve ICurrentUser desde la BD.
/// Sustitución futura: validar JWT de Supabase/Azure AD aquí.
/// </summary>
public sealed class DevAuthMiddleware(RequestDelegate next)
{
    // Usuarios demo hard-codeados para la pantalla de login
    private static readonly Dictionary<string, Guid> DemoEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        { "elena.castro@ceip-miguel-hernandez.es",   Guid.Parse("00000000-0000-0000-0000-000000000010") },
        { "laura.fernandez@ceip-miguel-hernandez.es", Guid.Parse("00000000-0000-0000-0000-000000000011") },
    };

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        // X-User-Id: resolución directa por GUID (tests de integración)
        var userIdHeader = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(userIdHeader) && Guid.TryParse(userIdHeader, out var directId))
        {
            var userById = await db.AppUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == directId);
            if (userById is not null)
                ctx.Items["CurrentUser"] = new CurrentUser(userById.Id, userById.SchoolId, userById.Role);
            await next(ctx);
            return;
        }

        // X-User-Email: resolución por email demo hardcodeado
        var email = ctx.Request.Headers["X-User-Email"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(email) &&
            DemoEmails.TryGetValue(email, out var userId))
        {
            var user = await db.AppUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is not null)
            {
                ctx.Items["CurrentUser"] = new CurrentUser(user.Id, user.SchoolId, user.Role);
            }
        }

        await next(ctx);
    }
}

// ── Extension methods ─────────────────────────────────────────────────────────
public static class CurrentUserExtensions
{
    /// <summary>Obtiene el usuario actual o lanza 401 si no está autenticado.</summary>
    public static ICurrentUser GetCurrentUserOrFail(this HttpContext ctx)
        => ctx.Items["CurrentUser"] as ICurrentUser
           ?? throw new UnauthorizedAccessException("No autenticado. Usa el header X-User-Email.");
}
