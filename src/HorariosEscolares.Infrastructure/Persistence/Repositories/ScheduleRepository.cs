using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Scheduling;
using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Infrastructure.Persistence.Repositories;

public sealed class ScheduleRepository(AppDbContext db) : IScheduleRepository
{
    public async Task AddScheduleWithDetailsAsync(
        ScheduleRecord schedule,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<ScheduleConflictRecord> conflicts,
        CancellationToken ct)
    {
        db.Schedules.Add(schedule);
        db.ScheduleEntries.AddRange(entries);
        db.ScheduleConflicts.AddRange(conflicts);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid scheduleId, CancellationToken ct)
    {
        await db.ScheduleConflicts.Where(c => c.ScheduleId == scheduleId).ExecuteDeleteAsync(ct);
        await db.ScheduleEntries.Where(e => e.ScheduleId == scheduleId).ExecuteDeleteAsync(ct);
        await db.Schedules.Where(s => s.Id == scheduleId).ExecuteDeleteAsync(ct);
    }
}
