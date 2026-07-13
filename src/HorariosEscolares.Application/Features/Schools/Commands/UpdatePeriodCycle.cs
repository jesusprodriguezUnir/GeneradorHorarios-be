using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record UpdatePeriodCycleCommand(
    Guid PeriodId,
    int Cycle,
    string MorningStart,
    string MorningEnd,
    string? AfternoonStart = null,
    string? AfternoonEnd = null,
    IReadOnlyList<CycleBreakDto>? Breaks = null) : IRequest<CycleScheduleDto>;

public sealed class UpdatePeriodCycleHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdatePeriodCycleCommand, CycleScheduleDto>
{
    public async Task<CycleScheduleDto> Handle(UpdatePeriodCycleCommand request, CancellationToken ct)
    {
        var period = await db.SchoolPeriods
            .FirstOrDefaultAsync(x => x.Id == request.PeriodId && x.SchoolId == user.SchoolId, ct)
            ?? throw new NotFoundException("Periodo no encontrado.");

        var cycle = await db.CycleSchedules
            .Include(x => x.Breaks)
            .FirstOrDefaultAsync(x => x.PeriodId == request.PeriodId && x.Cycle == request.Cycle, ct);

        var morningStart = TimeOnly.Parse(request.MorningStart);
        var morningEnd = TimeOnly.Parse(request.MorningEnd);
        var afternoonStart = request.AfternoonStart is not null ? TimeOnly.Parse(request.AfternoonStart) : (TimeOnly?)null;
        var afternoonEnd = request.AfternoonEnd is not null ? TimeOnly.Parse(request.AfternoonEnd) : (TimeOnly?)null;

        SchoolValidationHelper.ValidateCycleBoundaries(morningStart, morningEnd, afternoonStart, afternoonEnd);

        if (cycle is null)
        {
            cycle = new CycleSchedule
            {
                SchoolId = user.SchoolId,
                StageId = period.StageId,
                PeriodId = request.PeriodId,
                Cycle = request.Cycle,
                MorningStart = morningStart,
                MorningEnd = morningEnd,
                AfternoonStart = afternoonStart,
                AfternoonEnd = afternoonEnd,
                EndTime = afternoonEnd ?? morningEnd,
            };
            db.CycleSchedules.Add(cycle);
        }
        else
        {
            cycle.MorningStart = morningStart;
            cycle.MorningEnd = morningEnd;
            cycle.AfternoonStart = afternoonStart;
            cycle.AfternoonEnd = afternoonEnd;
            cycle.EndTime = afternoonEnd ?? morningEnd;
        }

        if (request.Breaks is not null)
        {
            db.CycleBreaks.RemoveRange(cycle.Breaks);
            db.CycleBreaks.AddRange(request.Breaks.Select(b => new CycleBreak
            {
                CycleScheduleId = cycle.Id,
                AfterSlot = b.AfterSlot,
                Minutes = b.Minutes,
            }));
        }

        await db.SaveChangesAsync(ct);

        var slots = SlotCalculator.Compute(cycle, period);
        return SchoolMapping.MapCycle(cycle, slots);
    }
}
