using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;

namespace HorariosEscolares.Application.Features.Roles;

public record GetRolesQuery : IRequest<IReadOnlyList<RoleResponse>>;
public record RoleResponse(Guid Id, string Code, string Name, string Kind, string? Description, int SortOrder);

internal sealed class GetRolesHandler(IAppDbContext db)
    : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<IReadOnlyList<RoleResponse>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        return await db.Roles.AsNoTracking()
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new RoleResponse(r.Id, r.Code, r.Name, r.Kind.ToString(), r.Description, r.SortOrder))
            .ToListAsync(ct);
    }
}
