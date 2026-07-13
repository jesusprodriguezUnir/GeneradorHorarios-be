using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Schools;

public interface ISchoolPeriodRepository
{
    Task<SchoolPeriod?> GetByIdAsync(Guid id, CancellationToken ct, bool includeCycles = true);
    Task<List<SchoolPeriod>> GetBySchoolAsync(Guid schoolId, CancellationToken ct);
    Task AddAsync(SchoolPeriod period, CancellationToken ct);
    void Delete(SchoolPeriod period);
    Task SaveChangesAsync(CancellationToken ct);
}
