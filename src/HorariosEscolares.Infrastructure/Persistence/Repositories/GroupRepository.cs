using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Groups;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class GroupRepository(AppDbContext db) : IGroupRepository
{
    public async Task<CourseGroup?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.CourseGroups.Include(g => g.SubjectHoursList).FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<CourseGroup>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
        => await db.CourseGroups.Where(g => g.SchoolId == schoolId).ToListAsync(ct);

    public Task AddAsync(CourseGroup group, CancellationToken ct)
    {
        db.CourseGroups.Add(group);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CourseGroup group, CancellationToken ct)
    {
        db.CourseGroups.Remove(group);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
