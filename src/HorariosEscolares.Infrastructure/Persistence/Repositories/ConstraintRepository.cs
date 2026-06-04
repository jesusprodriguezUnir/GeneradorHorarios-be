using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class ConstraintRepository(AppDbContext db) : IConstraintRepository
{
    public async Task<TeacherConstraint?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.TeacherConstraints.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<TeacherConstraint>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
        => await db.TeacherConstraints.Where(c => c.SchoolId == schoolId).ToListAsync(ct);

    public async Task<IReadOnlyList<TeacherConstraint>> GetByTeacherAsync(Guid teacherId, CancellationToken ct)
        => await db.TeacherConstraints.Where(c => c.TeacherId == teacherId).ToListAsync(ct);

    public Task AddAsync(TeacherConstraint constraint, CancellationToken ct)
    {
        db.TeacherConstraints.Add(constraint);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TeacherConstraint constraint, CancellationToken ct)
    {
        db.TeacherConstraints.Remove(constraint);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
