namespace HorariosEscolares.Application.Features.Schools;

public record SchoolStageDto(
    Guid Id, string StageType, string Name, int MinLevel, int MaxLevel, int SortOrder,
    string ScheduleType, string MorningStart, string? AfternoonStart,
    int SlotMinutes, int BreakAfterSlot, int BreakMinutes,
    int SlotsPerDay, int AfternoonSlots, int DaysPerWeek,
    IReadOnlyList<int> WorkingDays);
