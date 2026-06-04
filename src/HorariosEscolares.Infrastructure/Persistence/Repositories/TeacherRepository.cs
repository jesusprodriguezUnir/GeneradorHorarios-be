using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Teachers;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class TeacherRepository(AppDbContext db) : ITeacherRepository
{
    public async Task<Teacher?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Teacher>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
        => await db.Teachers.Where(t => t.SchoolId == schoolId).ToListAsync(ct);

    public Task AddAsync(Teacher teacher, CancellationToken ct)
    {
        db.Teachers.Add(teacher);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Teacher teacher, CancellationToken ct)
    {
        db.Teachers.Remove(teacher);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
