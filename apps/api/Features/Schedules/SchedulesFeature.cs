using Microsoft.AspNetCore.SignalR;
using MediatR;
using Hangfire;
using HorariosEscolares.Application.Features.Schedules;
using HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.BackgroundJobs;
using HorariosEscolares.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HorariosEscolares.Features.Schedules;

public sealed class GenerationProgressHub : Hub
{
    public async Task JoinSchoolGroup(string schoolId)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null) throw new HubException("No autorizado.");
        var user = httpContext.GetCurrentUserOrFail();
        if (user.SchoolId.ToString() != schoolId)
            throw new HubException("No autorizado para este colegio.");
        await Groups.AddToGroupAsync(Context.ConnectionId, schoolId);
    }

    public async Task LeaveSchoolGroup(string schoolId)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null) throw new HubException("No autorizado.");
        var user = httpContext.GetCurrentUserOrFail();
        if (user.SchoolId.ToString() != schoolId)
            throw new HubException("No autorizado para este colegio.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, schoolId);
    }
}

public record GenerateRequest(Guid StageId, Guid PeriodId, string AcademicYear, int TimeoutSeconds = 30);
public record UpdateEntryRequest(Guid TeacherId, Guid ClassroomId);

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/schedules");

        // GET /api/schedules — lista de horarios del colegio (filtro opcional por etapa)
        g.MapGet("/", async (ISender sender, Guid? stageId) =>
        {
            var result = await sender.Send(new GetSchedulesListQuery(stageId));
            return Results.Ok(result);
        });

        // POST /api/schedules/generate — encola job en background
        g.MapPost("/generate", async (
            HttpContext ctx,
            IBackgroundJobClient backgroundJobs,
            IAppDbContext db,
            GenerateRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var stageId = req.StageId;
            var periodId = req.PeriodId;

            if (stageId == Guid.Empty)
            {
                var firstStage = await db.SchoolStages.AsNoTracking()
                    .Where(s => s.SchoolId == user.SchoolId)
                    .OrderBy(s => s.SortOrder)
                    .FirstOrDefaultAsync();
                if (firstStage is not null)
                {
                    stageId = firstStage.Id;
                }
            }

            if (periodId == Guid.Empty && stageId != Guid.Empty)
            {
                var firstPeriod = await db.SchoolPeriods.AsNoTracking()
                    .Where(p => p.StageId == stageId)
                    .OrderByDescending(p => p.IsDefault)
                    .FirstOrDefaultAsync();
                if (firstPeriod is not null)
                {
                    periodId = firstPeriod.Id;
                }
            }

            var jobId = backgroundJobs.Enqueue<ScheduleGenerationJob>(job =>
                job.ExecuteAsync(
                    user.SchoolId,
                    stageId,
                    periodId,
                    req.AcademicYear,
                    req.TimeoutSeconds,
                    user.SchoolId.ToString(),
                    CancellationToken.None));

            return Results.Accepted($"/api/schedules/jobs/{jobId}", new { jobId });
        });

        // GET /api/schedules/jobs/{jobId} — estado de un job en background
        g.MapGet("/jobs/{jobId}", (string jobId) =>
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var job = monitor.JobDetails(jobId);
            if (job is null) return Results.NotFound();

            var latestState = job.History.OrderByDescending(h => h.CreatedAt).FirstOrDefault();
            return Results.Ok(new
            {
                id = jobId,
                state = latestState?.StateName ?? "Unknown",
                createdAt = job.CreatedAt,
            });
        });

        // GET /api/schedules/{id} — grid completo del horario
        g.MapGet("/{id:guid}", async (Guid id, ISender sender, HttpContext ctx) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var result = await sender.Send(new GetScheduleGridQuery(id));
            if (result is null) return Results.NotFound();
            if (user.IsTeacher && result.Status != "published") return Results.StatusCode(403);
            return Results.Ok(result);
        });

        // GET /api/schedules/me — horario del profesor autenticado
        g.MapGet("/me", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMyScheduleQuery());
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // POST /api/schedules/{id}/publish
        g.MapPost("/{id:guid}/publish", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var message = await sender.Send(new PublishScheduleCommand(id));
                return Results.Ok(new { message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // DELETE /api/schedules/{id} — elimina un horario (no publicado)
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                await sender.Send(new DeleteScheduleCommand(id));
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // PUT /api/schedules/{scheduleId}/entries/{entryId}
        g.MapPut("/{scheduleId:guid}/entries/{entryId:guid}", async (
            Guid scheduleId, Guid entryId,
            HttpContext ctx, ISender sender, UpdateEntryRequest req) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                await sender.Send(new UpdateScheduleEntryCommand(scheduleId, entryId,
                    new Application.Features.Schedules.UpdateEntryRequest(req.TeacherId, req.ClassroomId)));
                return Results.Ok(new { message = "Celda actualizada.", entryId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }
}
