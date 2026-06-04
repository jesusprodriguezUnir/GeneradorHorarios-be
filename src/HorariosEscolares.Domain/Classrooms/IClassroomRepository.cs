using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Classrooms;

public interface IClassroomRepository
{
    Task<Classroom?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Classroom>> GetBySchoolAsync(Guid schoolId, CancellationToken ct);
    Task AddAsync(Classroom classroom, CancellationToken ct);
    Task DeleteAsync(Classroom classroom, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
