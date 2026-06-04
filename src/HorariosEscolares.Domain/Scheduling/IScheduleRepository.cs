using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Scheduling;

public interface IScheduleRepository
{
    Task AddScheduleWithDetailsAsync(
        ScheduleRecord schedule,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<ScheduleConflictRecord> conflicts,
        CancellationToken ct);
}
