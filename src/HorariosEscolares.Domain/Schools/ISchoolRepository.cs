using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Schools;

public interface ISchoolRepository
{
    Task<School?> GetByIdAsync(Guid id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
