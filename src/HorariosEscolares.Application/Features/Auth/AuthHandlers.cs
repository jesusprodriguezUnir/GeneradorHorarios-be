using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Auth;

public record GetCurrentUserQuery : IRequest<CurrentUserResponse?>;
public record CurrentUserRole(Guid Id, string Code, string Name, string Kind);
public record CurrentUserResponse(
    Guid UserId, Guid SchoolId, CurrentUserRole Role,
    CurrentUserSchool? School, CurrentUserTeacher? Teacher);
public record CurrentUserSchool(Guid Id, string Name, string Slug);
public record CurrentUserTeacher(Guid Id, string FullName, string ColorKey, List<string> AssignedStageTypes);

public sealed class GetCurrentUserHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse?>
{
    public async Task<CurrentUserResponse?> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = currentUser;
        var school = await db.Schools.AsNoTracking()
            .Where(s => s.Id == user.SchoolId)
            .Select(s => new CurrentUserSchool(s.Id, s.Name, s.Slug))
            .FirstOrDefaultAsync(ct);

        CurrentUserTeacher? teacher = null;
        if (user.IsTeacher)
        {
            teacher = await db.Teachers.AsNoTracking()
                .Where(t => t.UserId == user.UserId)
                .Select(t => new CurrentUserTeacher(
                    t.Id,
                    t.FullName,
                    t.ColorKey,
                    t.StageAssignments.Select(sa => sa.Stage!.StageType).ToList()))
                .FirstOrDefaultAsync(ct);
        }

        var role = new CurrentUserRole(user.RoleId, user.RoleCode, user.RoleName, user.RoleKind.ToString());
        return new CurrentUserResponse(user.UserId, user.SchoolId, role, school, teacher);
    }
}

public record GetDemoUsersQuery : IRequest<List<DemoUserResponse>>;
public record DemoUserResponse(Guid Id, string Email, string FullName, CurrentUserRole Role);

public sealed class GetDemoUsersHandler(IAppDbContext db)
    : IRequestHandler<GetDemoUsersQuery, List<DemoUserResponse>>
{
    public async Task<List<DemoUserResponse>> Handle(GetDemoUsersQuery request, CancellationToken ct)
    {
        return await db.AppUsers.AsNoTracking()
            .Include(u => u.Role)
            .Select(u => new DemoUserResponse(
                u.Id,
                u.Email,
                u.FullName,
                new CurrentUserRole(u.Role!.Id, u.Role.Code, u.Role.Name, u.Role.Kind.ToString())))
            .ToListAsync(ct);
    }
}
