using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Scheduling;

public interface IScheduleRepository
{
    Task AddScheduleWithDetailsAsync(
        ScheduleRecord schedule,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<ScheduleConflictRecord> conflicts,
        CancellationToken ct);

    /// <summary>
    /// Elimina un horario y, en cascada explícita, sus celdas y conflictos.
    /// </summary>
    Task DeleteAsync(Guid scheduleId, CancellationToken ct);
}
