using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public abstract record GenerateScheduleResult
{
    public sealed record Success(
        Guid ScheduleId,
        string Status,
        int TotalAssigned,
        int TotalRequired,
        int ElapsedSeconds,
        int TotalConflicts,
        int TotalCost
    ) : GenerateScheduleResult;

    public sealed record ViabilityFailed(
        Guid ScheduleId,
        int TotalConflicts,
        IReadOnlyList<ConflictExplanation> Conflicts
    ) : GenerateScheduleResult;

    public sealed record NoAssignments() : GenerateScheduleResult;
}
