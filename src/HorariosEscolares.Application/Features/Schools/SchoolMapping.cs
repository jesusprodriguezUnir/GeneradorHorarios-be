using System.Text.Json;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schools;

public static class SchoolMapping
{
    public static SchoolDto MapSchool(School school, List<CycleSchedule> cycles)
    {
        var slots = SlotCalculator.Compute(school, school.SlotsPerDay);
        var workingDays = SlotCalculator.ParseWorkingDays(school.WorkingDays);
        var cycleDtos = cycles.OrderBy(c => c.Cycle)
            .Select(c => MapCycle(c, SlotCalculator.Compute(c, school)))
            .ToList();

        return new SchoolDto(
            school.Id, school.Name, school.Slug,
            school.CenterCode, school.Locality, school.Community,
            school.MinCourseLevel, school.MaxCourseLevel, school.AcademicYear,
            school.ScheduleType,
            school.MorningStart.ToString("HH:mm"), school.AfternoonStart?.ToString("HH:mm"),
            school.SlotMinutes, school.BreakAfterSlot, school.BreakMinutes,
            school.SlotsPerDay, school.AfternoonSlots, school.DaysPerWeek,
            workingDays,
            slots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            cycleDtos);
    }

    public static CycleScheduleDto MapCycle(CycleSchedule cycle, List<SlotInfo> computedSlots)
        => new(
            cycle.Cycle,
            cycle.MorningStart.ToString("HH:mm"),
            cycle.MorningEnd.ToString("HH:mm"),
            cycle.AfternoonStart?.ToString("HH:mm"),
            cycle.AfternoonEnd?.ToString("HH:mm"),
            cycle.EndTime.ToString("HH:mm"),
            computedSlots.Select(sl => new SlotDto(sl.Index, sl.StartTime, sl.EndTime, sl.IsBreak)).ToList(),
            cycle.Breaks.OrderBy(b => b.AfterSlot).Select(b => new CycleBreakDto(b.AfterSlot, b.Minutes)).ToList());

    public static SchoolStageDto MapStage(SchoolStage stage)
        => new(
            stage.Id, stage.StageType, stage.Name, stage.MinLevel, stage.MaxLevel, stage.SortOrder,
            stage.ScheduleType, stage.MorningStart.ToString("HH:mm"), stage.AfternoonStart?.ToString("HH:mm"),
            stage.SlotMinutes, stage.BreakAfterSlot, stage.BreakMinutes,
            stage.SlotsPerDay, stage.AfternoonSlots, stage.DaysPerWeek,
            SlotCalculator.ParseWorkingDays(stage.WorkingDays));

    public static SchoolPeriodDto MapPeriod(SchoolPeriod period)
    {
        var months = JsonSerializer.Deserialize<List<int>>(period.Months) ?? [];
        var cycleDtos = period.Cycles.OrderBy(c => c.Cycle)
            .Select(c => MapCycle(c, SlotCalculator.Compute(c, period)))
            .ToList();

        return new SchoolPeriodDto(
            period.Id, period.StageId, period.Key, period.Name, months,
            period.ScheduleType, period.SlotMinutes, period.SlotsPerDay, period.AfternoonSlots,
            period.IsDefault, period.SortOrder, cycleDtos);
    }
}
