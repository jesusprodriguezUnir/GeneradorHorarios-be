using Microsoft.AspNetCore.SignalR;
using Hangfire;
using HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Features.Schedules;

namespace HorariosEscolares.BackgroundJobs;

public sealed class ScheduleGenerationJob(
    IScheduleGenerationOrchestrator orchestrator,
    IHubContext<GenerationProgressHub> hub,
    ILogger<ScheduleGenerationJob> logger)
{
    private static readonly Action<ILogger, Exception?> LogWarningFailedSignalR =
        LoggerMessage.Define(LogLevel.Warning, 0, "Failed to send SignalR progress.");

    private static readonly Action<ILogger, Guid, Exception?> LogInformationCancelled =
        LoggerMessage.Define<Guid>(LogLevel.Information, 0, "Schedule generation cancelled for school {SchoolId}.");

    private static readonly Action<ILogger, Guid, Exception?> LogErrorFailed =
        LoggerMessage.Define<Guid>(LogLevel.Error, 0, "Schedule generation failed for school {SchoolId}.");

    [QueueAttribute("schedules")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(
        Guid schoolId,
        Guid stageId,
        Guid periodId,
        string academicYear,
        int timeoutSeconds,
        string signalRGroup,
        CancellationToken ct)
    {
        var progress = new Progress<GenerationProgress>(async p =>
        {
            try
            {
                await hub.Clients.Group(signalRGroup)
                    .SendAsync("Progress", new
                    {
                        assigned = p.Assigned,
                        total = p.Total,
                        percentage = p.Percentage,
                        currentAction = p.CurrentAction,
                    }, ct);
            }
            catch (Exception ex)
            {
                LogWarningFailedSignalR(logger, ex);
            }
        });

        try
        {
            var result = await orchestrator.GenerateAsync(
                schoolId, stageId, periodId, academicYear, timeoutSeconds, progress, ct);

            object payload = result switch
            {
                GenerateScheduleResult.Success s => new
                {
                    scheduleId = s.ScheduleId,
                    status = s.Status,
                    totalAssigned = s.TotalAssigned,
                    totalRequired = s.TotalRequired,
                    elapsedSeconds = s.ElapsedSeconds,
                    totalConflicts = s.TotalConflicts,
                    totalCost = s.TotalCost,
                },
                GenerateScheduleResult.ViabilityFailed f => new
                {
                    scheduleId = f.ScheduleId,
                    status = "failed",
                    totalAssigned = 0,
                    totalRequired = 0,
                    elapsedSeconds = 0,
                    totalConflicts = f.TotalConflicts,
                    conflicts = f.Conflicts.Select(c => new
                    {
                        type = c.Type.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture),
                        severity = c.Severity.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture),
                        description = c.Description,
                        suggestions = c.Suggestions,
                        teacherId = c.TeacherId,
                        groupId = c.GroupId,
                    }),
                },
                GenerateScheduleResult.NoAssignments => new { status = "no_assignments" },
                _ => new { status = "unknown" }
            };

            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", payload, ct);
        }
        catch (OperationCanceledException)
        {
            LogInformationCancelled(logger, schoolId, null);
            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", new { status = "cancelled" }, ct);
        }
        catch (Exception ex)
        {
            LogErrorFailed(logger, schoolId, ex);
            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", new { status = "error", message = ex.Message }, ct);
            throw;
        }
    }
}
