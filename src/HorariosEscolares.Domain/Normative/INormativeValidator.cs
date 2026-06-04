using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Domain.Normative;

public interface INormativeValidator
{
    Task<List<ConflictExplanation>> ValidateAsync(
        NormativeValidationData data,
        CancellationToken ct = default);
}

public record NormativeValidationData(
    SchoolConfig SchoolConfig,
    string Stage,
    int MinCourseLevel,
    int MaxCourseLevel,
    int BreakMinutes,
    int SlotMinutes,
    IReadOnlyList<NormativeAssignmentData> Assignments);

public record NormativeAssignmentData(
    Guid GroupId,
    string SubjectKey,
    string SubjectName,
    int WeeklyHours,
    int WeeklyHoursMin,
    int WeeklyHoursMax,
    int WeeklyHoursDefault);
