using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Assignments;

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Assignment>> GetBySchoolAsync(Guid schoolId, CancellationToken ct);
    Task<IReadOnlyList<Assignment>> GetByTeacherAsync(Guid teacherId, CancellationToken ct);
    Task AddAsync(Assignment assignment, CancellationToken ct);
    Task DeleteAsync(Assignment assignment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
