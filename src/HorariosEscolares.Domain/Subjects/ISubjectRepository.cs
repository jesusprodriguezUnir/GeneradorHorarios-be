using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Subjects;

public interface ISubjectRepository
{
    Task<SubjectAllocation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SubjectAllocation>> GetByTemplateAsync(Guid templateId, CancellationToken ct);
    Task AddAsync(SubjectAllocation allocation, CancellationToken ct);
    Task DeleteAsync(SubjectAllocation allocation, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
