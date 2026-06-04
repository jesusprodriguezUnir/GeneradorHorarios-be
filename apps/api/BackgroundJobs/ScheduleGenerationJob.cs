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
    [QueueAttribute("schedules")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(
        Guid schoolId,
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
                logger.LogWarning(ex, "Failed to send SignalR progress.");
            }
        });

        try
        {
            var result = await orchestrator.GenerateAsync(
                schoolId, periodId, academicYear, timeoutSeconds, progress, ct);

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
                        type = c.Type.ToString().ToLower(),
                        severity = c.Severity.ToString().ToLower(),
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
            logger.LogInformation("Schedule generation cancelled for school {SchoolId}.", schoolId);
            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", new { status = "cancelled" }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Schedule generation failed for school {SchoolId}.", schoolId);
            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", new { status = "error", message = ex.Message }, ct);
            throw;
        }
    }
}
