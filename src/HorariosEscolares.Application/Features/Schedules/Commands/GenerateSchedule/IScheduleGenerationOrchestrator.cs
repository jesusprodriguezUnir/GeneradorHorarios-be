using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public interface IScheduleGenerationOrchestrator
{
    Task<GenerateScheduleResult> GenerateAsync(
        Guid schoolId,
        Guid periodId,
        string academicYear,
        int timeoutSeconds,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct);
}
