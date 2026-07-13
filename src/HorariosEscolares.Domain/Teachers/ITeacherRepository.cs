using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Teachers;

public interface ITeacherRepository
{
    Task<Teacher?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Teacher>> GetBySchoolAsync(Guid schoolId, CancellationToken ct);
    Task AddAsync(Teacher teacher, CancellationToken ct);
    Task DeleteAsync(Teacher teacher, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
