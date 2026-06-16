using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Schools;

public record GetPeriodQuery(Guid PeriodId) : IRequest<SchoolPeriodDto?>;

public sealed class GetPeriodHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodQuery, SchoolPeriodDto?>
{
    public async Task<SchoolPeriodDto?> Handle(GetPeriodQuery request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.Id == request.PeriodId && x.SchoolId == user.SchoolId, ct);
        return period is null ? null : SchoolMapping.MapPeriod(period);
    }
}
