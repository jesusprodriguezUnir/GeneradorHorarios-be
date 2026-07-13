using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Groups;

public interface IGroupRepository
{
    Task<CourseGroup?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CourseGroup>> GetBySchoolAsync(Guid schoolId, CancellationToken ct);
    Task AddAsync(CourseGroup group, CancellationToken ct);
    Task DeleteAsync(CourseGroup group, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
