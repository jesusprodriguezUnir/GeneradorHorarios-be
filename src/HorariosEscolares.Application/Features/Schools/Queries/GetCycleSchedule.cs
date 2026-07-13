using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record GetCycleScheduleQuery(int Cycle) : IRequest<CycleScheduleDto?>;

public sealed class GetCycleScheduleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetCycleScheduleQuery, CycleScheduleDto?>
{
    public async Task<CycleScheduleDto?> Handle(GetCycleScheduleQuery request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolId == user.SchoolId && x.IsDefault, ct);
        if (period is null) return null;

        var cycle = await db.CycleSchedules.AsNoTracking()
            .Include(c => c.Breaks)
            .FirstOrDefaultAsync(x => x.PeriodId == period.Id && x.Cycle == request.Cycle, ct);
        if (cycle is null) return null;

        var slots = SlotCalculator.Compute(cycle, period);
        return SchoolMapping.MapCycle(cycle, slots);
    }
}
