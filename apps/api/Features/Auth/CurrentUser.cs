namespace HorariosEscolares.Features.Auth;

// ── Contrato de usuario actual ────────────────────────────────────────────────
// Puente a Supabase Auth: hoy se resuelve con cabeceras dev, después con JWT.

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SchoolId { get; }
    string Role { get; }
    bool IsAdmin { get; }
    bool IsTeacher { get; }
}

public sealed class CurrentUser : ICurrentUser
{
    public Guid UserId { get; }
    public Guid SchoolId { get; }
    public string Role { get; }
    public bool IsAdmin => Role == "school_admin";
    public bool IsTeacher => Role == "teacher";

    public CurrentUser(Guid userId, Guid schoolId, string role)
        => (UserId, SchoolId, Role) = (userId, schoolId, role);
}
