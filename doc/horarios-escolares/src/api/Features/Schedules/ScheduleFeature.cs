using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using Microsoft.AspNetCore.SignalR;

namespace HorariosEscolares.Features.Schedules;

// ── Endpoint registration ─────────────────────────────────────────────────────

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedules").RequireAuthorization();

        group.MapPost("/generate", GenerateScheduleEndpoint.Handle)
             .WithName("GenerateSchedule")
             .WithSummary("Genera el horario de un colegio")
             .Produces<GenerateScheduleResponse>(200)
             .Produces(400)
             .Produces(403);

        group.MapGet("/me", GetMyScheduleEndpoint.Handle)
             .WithName("GetMySchedule")
             .WithSummary("Devuelve el horario del profesor autenticado")
             .Produces<MyScheduleResponse>(200)
             .Produces(404);

        group.MapPost("/{scheduleId}/publish", PublishScheduleEndpoint.Handle)
             .WithName("PublishSchedule")
             .WithSummary("Publica un horario generado")
             .Produces(200)
             .Produces(400)
             .Produces(403);

        return app;
    }
}

// ── Generate — Command ────────────────────────────────────────────────────────

public record GenerateScheduleCommand(
    Guid SchoolId,
    string AcademicYear,
    int TimeoutSeconds = 30);

public record GenerateScheduleResponse(
    Guid ScheduleId,
    string Status,
    int TotalAssigned,
    int TotalRequired,
    int ElapsedSeconds,
    IReadOnlyList<ConflictDto> Conflicts);

public record ConflictDto(
    string Type,
    string Severity,
    string Description,
    string[] Suggestions);

// ── Generate — Handler ────────────────────────────────────────────────────────

public static class GenerateScheduleEndpoint
{
    public static async Task<IResult> Handle(
        GenerateScheduleCommand command,
        IScheduleEngine engine,
        IHubContext<GenerationProgressHub> hub,
        // IScheduleRepository repo,  // se añade en SPEC-003
        CancellationToken ct)
    {
        // TODO (SPEC-003): cargar configuración del colegio desde BD
        // var config = await repo.GetSchoolConfigAsync(command.SchoolId, ct);
        // var sessions = await repo.GetSessionsToAssignAsync(command.SchoolId, ct);

        // Placeholder para el piloto — contexto mínimo
        var context = new GenerationContext
        {
            School = new SchoolConfig(6, []),
            Sessions = [],
            HardConstraints = [],
            SoftConstraints = [],
            TimeoutSeconds = command.TimeoutSeconds
        };

        var progress = new Progress<GenerationProgress>(async p =>
        {
            await hub.Clients
                .Group(command.SchoolId.ToString())
                .SendAsync("Progress", p, ct);
        });

        var result = await engine.GenerateAsync(context, ct, progress);

        // TODO (SPEC-003): persistir resultado en BD

        return Results.Ok(new GenerateScheduleResponse(
            Guid.NewGuid(),
            result.Status.ToString(),
            result.TotalAssigned,
            result.TotalRequired,
            result.ElapsedSeconds,
            result.Conflicts.Select(c => new ConflictDto(
                c.Type.ToString(),
                c.Severity.ToString(),
                c.Description,
                [.. c.Suggestions]
            )).ToList()
        ));
    }
}

// ── GetMySchedule — Handler ───────────────────────────────────────────────────

public record MyScheduleResponse(
    string TeacherName,
    string SchoolName,
    string AcademicYear,
    IReadOnlyList<ScheduleEntryDto> Entries,
    IReadOnlyList<SlotDto> Slots);

public record ScheduleEntryDto(
    int DayOfWeek,
    int SlotIndex,
    string SlotTime,
    string SubjectName,
    string GroupLabel,
    string ClassroomName);

public record SlotDto(
    int Index,
    string StartTime,
    string EndTime,
    bool IsBreak);

public static class GetMyScheduleEndpoint
{
    public static async Task<IResult> Handle(
        HttpContext ctx,
        // IScheduleRepository repo,  // se añade en SPEC-003
        CancellationToken ct)
    {
        // TODO (SPEC-011): extraer userId del JWT de Supabase
        // var userId = ctx.User.GetSupabaseUserId();
        // var schedule = await repo.GetPublishedScheduleForTeacherAsync(userId, ct);

        // Placeholder
        await Task.CompletedTask;
        return Results.NotFound(new { message = "No hay horario publicado para tu colegio" });
    }
}

// ── Publish — Handler ─────────────────────────────────────────────────────────

public static class PublishScheduleEndpoint
{
    public static async Task<IResult> Handle(
        Guid scheduleId,
        // IScheduleRepository repo,
        CancellationToken ct)
    {
        // TODO (SPEC-036): publicar horario y archivar el anterior
        await Task.CompletedTask;
        return Results.Ok(new { message = "Horario publicado correctamente" });
    }
}

// ── SignalR Hub ───────────────────────────────────────────────────────────────

public sealed class GenerationProgressHub : Hub
{
    public async Task JoinSchoolGroup(string schoolId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, schoolId);

    public async Task LeaveSchoolGroup(string schoolId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, schoolId);
}
