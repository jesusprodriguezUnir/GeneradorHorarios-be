using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SchoolId { get; }
    Guid RoleId { get; }
    string RoleCode { get; }
    string RoleName { get; }
    RoleKind RoleKind { get; }
    bool IsAdmin { get; }
    bool IsTeacher { get; }
}
