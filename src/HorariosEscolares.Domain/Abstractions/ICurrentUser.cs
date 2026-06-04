namespace HorariosEscolares.Domain.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SchoolId { get; }
    string Role { get; }
    bool IsAdmin { get; }
    bool IsTeacher { get; }
}
