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
        // Leer email del header (enviado por el frontend en modo dev)
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
