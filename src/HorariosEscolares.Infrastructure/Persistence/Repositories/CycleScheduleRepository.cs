using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class CycleScheduleRepository(AppDbContext db) : ICycleScheduleRepository
{
    public async Task<CycleSchedule?> GetBySchoolAndCycleAsync(Guid schoolId, int cycle, CancellationToken ct, bool includeBreaks = true)
    {
        var query = db.CycleSchedules.AsQueryable();
        if (includeBreaks)
            query = query.Include(c => c.Breaks);
        return await query.FirstOrDefaultAsync(c => c.SchoolId == schoolId && c.Cycle == cycle, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
