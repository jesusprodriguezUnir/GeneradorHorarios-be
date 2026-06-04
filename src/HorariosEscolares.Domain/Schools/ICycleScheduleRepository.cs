using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Schools;

public interface ICycleScheduleRepository
{
    Task<CycleSchedule?> GetBySchoolAndCycleAsync(Guid schoolId, int cycle, CancellationToken ct, bool includeBreaks = true);
    Task SaveChangesAsync(CancellationToken ct);
}
