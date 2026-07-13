using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public record CreateStageCommand(string StageType) : IRequest<SchoolStageDto>;

public sealed class CreateStageHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CreateStageCommand, SchoolStageDto>
{
    public async Task<SchoolStageDto> Handle(CreateStageCommand request, CancellationToken ct)
    {
        var stageType = request.StageType.ToLowerInvariant();

        var exists = await db.SchoolStages
            .AnyAsync(st => st.SchoolId == user.SchoolId && st.StageType == stageType, ct);
        if (exists)
            throw new InvalidOperationException($"La etapa '{stageType}' ya existe en este centro.");

        var defaults = GetStageDefaults(stageType);

        var newStage = new SchoolStage
        {
            SchoolId = user.SchoolId,
            StageType = stageType,
            Name = defaults.Name,
            MinLevel = defaults.MinLevel,
            MaxLevel = defaults.MaxLevel,
            SortOrder = defaults.SortOrder,
            SlotsPerDay = defaults.SlotsPerDay,
            MorningStart = defaults.MorningStart,
            BreakAfterSlot = defaults.BreakAfterSlot,
            ScheduleType = "continua",
            AfternoonStart = null,
            SlotMinutes = 60,
            BreakMinutes = 30,
            DaysPerWeek = 5,
            WorkingDays = "[1,2,3,4,5]"
        };

        db.SchoolStages.Add(newStage);

        var newPeriod = new SchoolPeriod
        {
            SchoolId = user.SchoolId,
            StageId = newStage.Id,
            Key = "ordinario",
            Name = "Jornada ordinaria",
            Months = "[10,11,12,1,2,3,4,5]",
            ScheduleType = newStage.ScheduleType,
            SlotMinutes = newStage.SlotMinutes,
            SlotsPerDay = newStage.SlotsPerDay,
            AfternoonSlots = newStage.AfternoonSlots,
            IsDefault = true,
            SortOrder = 0
        };

        var breaksList = newStage.BreakAfterSlot >= 0 && newStage.BreakMinutes > 0
            ? new[] { (newStage.BreakAfterSlot, newStage.BreakMinutes) }
            : Array.Empty<(int, int)>();

        var cycleEnd = SlotCalculator.ComputeEndTime(
            totalSlots: newPeriod.SlotsPerDay,
            slotMinutes: newPeriod.SlotMinutes,
            breaks: breaksList,
            afternoonSlots: newPeriod.AfternoonSlots,
            morningStart: newStage.MorningStart,
            afternoonStart: null,
            isPartida: false);

        for (int c = 1; c <= 3; c++)
        {
            var cycleSchedule = new CycleSchedule
            {
                SchoolId = user.SchoolId,
                StageId = newStage.Id,
                PeriodId = newPeriod.Id,
                Cycle = c,
                MorningStart = newStage.MorningStart,
                EndTime = cycleEnd,
                AfternoonStart = null
            };

            if (newStage.BreakAfterSlot >= 0 && newStage.BreakMinutes > 0)
            {
                cycleSchedule.Breaks.Add(new CycleBreak
                {
                    CycleScheduleId = cycleSchedule.Id,
                    AfterSlot = newStage.BreakAfterSlot,
                    Minutes = newStage.BreakMinutes
                });
            }

            newPeriod.Cycles.Add(cycleSchedule);
        }

        db.SchoolPeriods.Add(newPeriod);
        await db.SaveChangesAsync(ct);

        return SchoolMapping.MapStage(newStage);
    }

    private static StageDefaults GetStageDefaults(string stageType) => stageType switch
    {
        StageTypes.Infantil => new("Educación Infantil", 1, 2, 0, 5, new TimeOnly(9, 0), 2),
        StageTypes.Primaria => new("Educación Primaria", 1, 6, 1, 5, new TimeOnly(9, 0), 2),
        StageTypes.Secundaria => new("Educación Secundaria (ESO)", 1, 4, 2, 6, new TimeOnly(8, 30), 3),
        _ => new(char.ToUpper(stageType[0]) + stageType[1..], 1, 6, 3, 5, new TimeOnly(9, 0), 2)
    };

    private sealed record StageDefaults(
        string Name, int MinLevel, int MaxLevel, int SortOrder,
        int SlotsPerDay, TimeOnly MorningStart, int BreakAfterSlot);
}
