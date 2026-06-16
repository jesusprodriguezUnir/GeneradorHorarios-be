using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record GetPeriodCycleQuery(Guid PeriodId, int Cycle) : IRequest<CycleScheduleDto?>;

public sealed class GetPeriodCycleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPeriodCycleQuery, CycleScheduleDto?>
{
    public async Task<CycleScheduleDto?> Handle(GetPeriodCycleQuery request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PeriodId && x.SchoolId == user.SchoolId, ct);
        if (period is null) return null;

        var cycle = await db.CycleSchedules.AsNoTracking()
            .Include(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.PeriodId == request.PeriodId && x.Cycle == request.Cycle, ct);
        if (cycle is null) return null;

        var slots = SlotCalculator.Compute(cycle, period);
        return SchoolMapping.MapCycle(cycle, slots);
    }
}
