using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Constraints;

public interface IConstraintRepository
{
    Task<TeacherConstraint?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TeacherConstraint>> GetBySchoolAsync(Guid schoolId, CancellationToken ct);
    Task<IReadOnlyList<TeacherConstraint>> GetByTeacherAsync(Guid teacherId, CancellationToken ct);
    Task AddAsync(TeacherConstraint constraint, CancellationToken ct);
    Task DeleteAsync(TeacherConstraint constraint, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
