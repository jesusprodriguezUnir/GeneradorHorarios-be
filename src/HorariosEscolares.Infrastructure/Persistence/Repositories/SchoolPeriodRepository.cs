using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class SchoolPeriodRepository(AppDbContext db) : ISchoolPeriodRepository
{
    public async Task<SchoolPeriod?> GetByIdAsync(Guid id, CancellationToken ct, bool includeCycles = true)
    {
        var query = db.SchoolPeriods.AsQueryable();
        if (includeCycles)
            query = query.Include(p => p.Cycles).ThenInclude(c => c.Breaks);
        return await query.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<SchoolPeriod>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
    {
        return await db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .Where(p => p.SchoolId == schoolId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SchoolPeriod period, CancellationToken ct)
        => await db.SchoolPeriods.AddAsync(period, ct);

    public void Delete(SchoolPeriod period)
        => db.SchoolPeriods.Remove(period);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
