using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Features.Auth;

public sealed class DevAuthMiddleware(RequestDelegate next)
{
    private static readonly Dictionary<string, Guid> DemoEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        { "elena.castro@ceip-miguel-hernandez.es",   Guid.Parse("00000000-0000-0000-0000-000000000010") },
        { "laura.fernandez@ceip-miguel-hernandez.es", Guid.Parse("00000000-0000-0000-0000-000000000011") },
    };

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        var userIdHeader = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(userIdHeader) && Guid.TryParse(userIdHeader, out var directId))
        {
            var userById = await db.AppUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == directId);
            if (userById is not null)
                ctx.Items["CurrentUser"] = new CurrentUserDto(userById.Id, userById.SchoolId, userById.Role);
            await next(ctx);
            return;
        }

        var email = ctx.Request.Headers["X-User-Email"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(email) &&
            DemoEmails.TryGetValue(email, out var userId))
        {
            var user = await db.AppUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user is not null)
                ctx.Items["CurrentUser"] = new CurrentUserDto(user.Id, user.SchoolId, user.Role);
        }

        await next(ctx);
    }
}

public sealed class CurrentUserDto(Guid userId, Guid schoolId, string role) : ICurrentUser
{
    public Guid UserId { get; } = userId;
    public Guid SchoolId { get; } = schoolId;
    public string Role { get; } = role;
    public bool IsAdmin => Role == "school_admin";
    public bool IsTeacher => Role == "teacher";
}

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : Domain.Abstractions.ICurrentUser
{
    private ICurrentUser? User => httpContextAccessor.HttpContext?.Items["CurrentUser"] as ICurrentUser;
    public Guid UserId => User?.UserId ?? throw new UnauthorizedAccessException("No autenticado");
    public Guid SchoolId => User?.SchoolId ?? throw new UnauthorizedAccessException("No autenticado");
    public string Role => User?.Role ?? "";
    public bool IsAdmin => User?.IsAdmin ?? false;
    public bool IsTeacher => User?.IsTeacher ?? false;
}

public static class CurrentUserExtensions
{
    public static ICurrentUser GetCurrentUserOrFail(this HttpContext ctx)
        => ctx.Items["CurrentUser"] as ICurrentUser
           ?? throw new UnauthorizedAccessException("No autenticado. Usa el header X-User-Email.");
}
