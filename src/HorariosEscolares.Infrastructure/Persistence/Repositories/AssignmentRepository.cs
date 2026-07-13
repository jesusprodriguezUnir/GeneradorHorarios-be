using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Assignments;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class AssignmentRepository(AppDbContext db) : IAssignmentRepository
{
    public async Task<Assignment?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Assignments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Assignment>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
        => await db.Assignments.Where(a => a.SchoolId == schoolId).ToListAsync(ct);

    public async Task<IReadOnlyList<Assignment>> GetByTeacherAsync(Guid teacherId, CancellationToken ct)
        => await db.Assignments.Where(a => a.TeacherId == teacherId).ToListAsync(ct);

    public Task AddAsync(Assignment assignment, CancellationToken ct)
    {
        db.Assignments.Add(assignment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Assignment assignment, CancellationToken ct)
    {
        db.Assignments.Remove(assignment);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
