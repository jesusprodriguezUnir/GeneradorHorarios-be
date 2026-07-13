namespace HorariosEscolares.Application.Features.Schools;

public record SchoolDto(
    Guid Id, string Name, string Slug,
    string? CenterCode, string? Locality, string Community,
    int MinCourseLevel, int MaxCourseLevel, string AcademicYear,
    string ScheduleType, string MorningStart, string? AfternoonStart,
    int SlotMinutes, int BreakAfterSlot, int BreakMinutes,
    int SlotsPerDay, int AfternoonSlots, int DaysPerWeek,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<SlotDto> ComputedSlots,
    IReadOnlyList<CycleScheduleDto> Cycles);

public record NormativeCheckDto(
    bool IsCompliant, int ErrorCount, int WarningCount,
    IReadOnlyList<NormativeIssueDto> Issues);

public record NormativeIssueDto(string Severity, string Description, IReadOnlyList<string> Suggestions, Guid? GroupId);
