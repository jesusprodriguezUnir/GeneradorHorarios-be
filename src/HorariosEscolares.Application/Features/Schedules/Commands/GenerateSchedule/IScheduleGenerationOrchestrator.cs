using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public interface IScheduleGenerationOrchestrator
{
    Task<GenerateScheduleResult> GenerateAsync(
        Guid schoolId,
        Guid stageId,
        Guid periodId,
        string academicYear,
        Guid createdBy,
        int timeoutSeconds,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct);
}
