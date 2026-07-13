using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Classrooms;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class ClassroomRepository(AppDbContext db) : IClassroomRepository
{
    public async Task<Classroom?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Classrooms.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Classroom>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
        => await db.Classrooms.Where(c => c.SchoolId == schoolId).ToListAsync(ct);

    public Task AddAsync(Classroom classroom, CancellationToken ct)
    {
        db.Classrooms.Add(classroom);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Classroom classroom, CancellationToken ct)
    {
        db.Classrooms.Remove(classroom);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
