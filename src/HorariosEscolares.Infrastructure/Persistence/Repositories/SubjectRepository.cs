using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Subjects;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class SubjectRepository(AppDbContext db) : ISubjectRepository
{
    public async Task<SubjectAllocation?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.SubjectAllocations.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<SubjectAllocation>> GetByTemplateAsync(Guid templateId, CancellationToken ct)
        => await db.SubjectAllocations.Where(a => a.TemplateId == templateId).ToListAsync(ct);

    public Task AddAsync(SubjectAllocation allocation, CancellationToken ct)
    {
        db.SubjectAllocations.Add(allocation);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SubjectAllocation allocation, CancellationToken ct)
    {
        db.SubjectAllocations.Remove(allocation);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
