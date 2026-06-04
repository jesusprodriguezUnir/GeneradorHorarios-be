using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class SchoolRepository(AppDbContext db) : ISchoolRepository
{
    public async Task<School?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Schools.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
