using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Schools;

public record GetSchoolQuery : IRequest<SchoolDto?>;

public sealed class GetSchoolHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetSchoolQuery, SchoolDto?>
{
    public async Task<SchoolDto?> Handle(GetSchoolQuery request, CancellationToken ct)
    {
        var school = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == user.SchoolId, ct);
        if (school is null) return null;

        var defaultPeriod = await db.SchoolPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SchoolId == user.SchoolId && p.IsDefault, ct);
        var cycles = defaultPeriod is not null
            ? await db.CycleSchedules.AsNoTracking()
                .Include(c => c.Breaks)
                .Where(c => c.PeriodId == defaultPeriod.Id).ToListAsync(ct)
            : [];

        return SchoolMapping.MapSchool(school, cycles);
    }
}
