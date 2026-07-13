using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Domain.Normative;

public interface INormativeValidator
{
    Task<List<ConflictExplanation>> ValidateAsync(
        NormativeValidationData data,
        CancellationToken ct = default);
}

public class NormativeValidationData
{
    public SchoolConfig SchoolConfig { get; init; } = new(0, 0, [], [], []);
    public string Stage { get; init; } = "primaria";
    public int MinCourseLevel { get; init; }
    public int MaxCourseLevel { get; init; }
    public int BreakMinutes { get; init; }
    public int SlotMinutes { get; init; }
    public IReadOnlyList<NormativeAssignmentData> Assignments { get; init; } = [];
    public bool EnforceWeeklyLectiveMinimum { get; init; } = true;
}

public record NormativeAssignmentData(
    Guid GroupId,
    string SubjectKey,
    string SubjectName,
    int WeeklyHours,
    int WeeklyHoursMin,
    int WeeklyHoursMax,
    int WeeklyHoursDefault);
