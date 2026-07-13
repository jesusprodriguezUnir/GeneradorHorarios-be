using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Features.Auth;

public sealed class DevAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, IConfiguration config)
    {
        // ── 1. API key secreta (X-Api-Key) ─────────────────────────────────────
        var configuredKey = config["ApiKey:Key"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var incomingKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(incomingKey) && incomingKey == configuredKey)
            {
                var apiKeyEmail = config["ApiKey:Email"] ?? string.Empty;
                var apiKeyUser = await db.AppUsers.AsNoTracking()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == apiKeyEmail);
                if (apiKeyUser is not null && apiKeyUser.Role is not null)
                    ctx.Items["CurrentUser"] = new CurrentUserDto(apiKeyUser.Id, apiKeyUser.SchoolId, apiKeyUser.RoleId, apiKeyUser.Role);
                await next(ctx);
                return;
            }
        }

        // ── 2. User-Id directo ─────────────────────────────────────────────────
        var userIdHeader = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(userIdHeader) && Guid.TryParse(userIdHeader, out var directId))
        {
            var userById = await db.AppUsers.AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == directId);
            if (userById is not null && userById.Role is not null)
                ctx.Items["CurrentUser"] = new CurrentUserDto(userById.Id, userById.SchoolId, userById.RoleId, userById.Role);
            await next(ctx);
            return;
        }

        var email = ctx.Request.Headers["X-User-Email"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await db.AppUsers.AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);
            if (user is not null && user.Role is not null)
                ctx.Items["CurrentUser"] = new CurrentUserDto(user.Id, user.SchoolId, user.RoleId, user.Role);
        }

        await next(ctx);
    }
}

public sealed class CurrentUserDto(Guid userId, Guid schoolId, Guid roleId, Role role) : ICurrentUser
{
    public Guid UserId { get; } = userId;
    public Guid SchoolId { get; } = schoolId;
    public Guid RoleId { get; } = roleId;
    public string RoleCode { get; } = role.Code;
    public string RoleName { get; } = role.Name;
    public RoleKind RoleKind { get; } = role.Kind;
    public bool IsAdmin => RoleKind == RoleKind.Admin;
    public bool IsTeacher => RoleKind == RoleKind.Teacher;
}

/// <summary>
/// Tenant del request actual para los query filters globales de <see cref="AppDbContext"/>.
/// Devuelve null fuera de un request autenticado (seed, Hangfire, el propio middleware de auth).
/// </summary>
public sealed class HttpTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public Guid? SchoolId =>
        (httpContextAccessor.HttpContext?.Items["CurrentUser"] as ICurrentUser)?.SchoolId;
}

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : Domain.Abstractions.ICurrentUser
{
    private ICurrentUser? User => httpContextAccessor.HttpContext?.Items["CurrentUser"] as ICurrentUser;
    public Guid UserId => User?.UserId ?? throw new UnauthorizedAccessException("No autenticado");
    public Guid SchoolId => User?.SchoolId ?? throw new UnauthorizedAccessException("No autenticado");
    public Guid RoleId => User?.RoleId ?? throw new UnauthorizedAccessException("No autenticado");
    public string RoleCode => User?.RoleCode ?? "";
    public string RoleName => User?.RoleName ?? "";
    public RoleKind RoleKind => User?.RoleKind ?? throw new UnauthorizedAccessException("No autenticado");
    public bool IsAdmin => User?.IsAdmin ?? false;
    public bool IsTeacher => User?.IsTeacher ?? false;
}

public static class CurrentUserExtensions
{
    public static ICurrentUser GetCurrentUserOrFail(this HttpContext ctx)
        => ctx.Items["CurrentUser"] as ICurrentUser
           ?? throw new UnauthorizedAccessException("No autenticado. Usa el header X-User-Email.");
}
