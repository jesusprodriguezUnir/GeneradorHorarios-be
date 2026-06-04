using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Services;

public interface IScheduleEngine
{
    Task<ScheduleResult> GenerateAsync(
        GenerationContext context,
        CancellationToken cancellationToken,
        IProgress<GenerationProgress>? progress = null);
}

public record SoftConstraintWeights
{
    public int NoIntensiveSubjectLastSlot { get; init; } = 7;
    public int DistributeSubjectAcrossDays { get; init; } = 5;
    public int TeacherConsecutiveLoad { get; init; } = 6;
    public int TeacherGaps { get; init; } = 4;
    public int ConsecutiveBlockPreference { get; init; } = 5;
}

public record GenerationContext
{
    public required SchoolConfig School { get; init; }
    public required IReadOnlyList<SessionToAssign> Sessions { get; init; }
    public required IReadOnlyList<IHardConstraint> HardConstraints { get; init; }
    public required IReadOnlyList<ISoftConstraint> SoftConstraints { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public SoftConstraintWeights Weights { get; init; } = new();
}

public record SessionToAssign(
    Guid AssignmentId,
    Guid GroupId,
    Guid TeacherId,
    Guid AllocationId,
    string SubjectName,
    string GroupLabel,
    Guid? RequiredClassroomId,
    ClassroomType? RequiredClassroomType,
    int MaxConsecutiveSlots,
    bool RequiresSpecialist,
    IReadOnlyList<string> TeacherSpecialties,
    string SubjectKey,
    int TeacherMaxWeeklyHours,
    bool SplittableAcrossDays,
    int Cycle = 1);

public record ScheduleResult
{
    public required GenerationStatus Status { get; init; }
    public required IReadOnlyList<AssignedSlot> AssignedSlots { get; init; }
    public required IReadOnlyList<ConflictExplanation> Conflicts { get; init; }
    public int ElapsedSeconds { get; init; }
    public int TotalAssigned => AssignedSlots.Count;
    public int TotalRequired { get; init; }
    public int TotalCost { get; init; }
}

public record AssignedSlot(
    Guid AssignmentId,
    Guid GroupId,
    Guid TeacherId,
    Guid AllocationId,
    Guid ClassroomId,
    int DayOfWeek,
    int SlotIndex,
    int StartMinute = 0,
    int EndMinute = 0,
    int Cycle = 1);

public record ConflictExplanation
{
    public required ConflictType Type { get; init; }
    public required ConflictSeverity Severity { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Suggestions { get; init; } = [];
    public Guid? GroupId { get; init; }
    public Guid? TeacherId { get; init; }
    public int? DayOfWeek { get; init; }
    public int? SlotIndex { get; init; }
}

public record GenerationProgress(
    int Assigned,
    int Total,
    string CurrentAction)
{
    public int Percentage => Total == 0 ? 0 : Assigned * 100 / Total;
}

public enum GenerationStatus { Complete, Partial, Failed }
public enum ClassroomType { Regular, Gym, Music, Lab, IT, Support }
