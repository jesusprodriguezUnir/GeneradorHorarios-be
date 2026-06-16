namespace HorariosEscolares.Application.Features.Schools;

public record SchoolPeriodDto(
    Guid Id, Guid StageId, string Key, string Name, IReadOnlyList<int> Months,
    string ScheduleType, int SlotMinutes, int SlotsPerDay, int AfternoonSlots,
    bool IsDefault, int SortOrder, IReadOnlyList<CycleScheduleDto> Cycles);
