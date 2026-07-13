using MediatR;
using HorariosEscolares.Application.Features.Subjects;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Subjects;

public static class SubjectEndpoints
{
    public static IEndpointRouteBuilder MapSubjectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/subjects");

        g.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAllSubjectsQuery());
            return Results.Ok(result);
        });

        g.MapPost("/clone-official", async (ISender sender) =>
        {
            try
            {
                var result = await sender.Send(new CloneOfficialCurriculumCommand());
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAdmin();

        g.MapPut("/{id:guid}/hours", async (Guid id, ISender sender, UpdateSubjectHoursCommand cmd) =>
        {
            try
            {
                var result = await sender.Send(cmd with { Id = id });
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAdmin();

        g.MapPost("/", async (ISender sender, CreateSubjectCommand cmd) =>
        {
            var result = await sender.Send(cmd);
            return Results.Created($"/api/subjects/{result.Id}", result);
        }).RequireAdmin();

        g.MapPut("/{id:guid}", async (Guid id, ISender sender, UpdateSubjectCommand cmd) =>
        {
            try
            {
                var result = await sender.Send(cmd with { Id = id });
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
        }).RequireAdmin();

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            try
            {
                await sender.Send(new DeleteSubjectCommand(id));
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
        }).RequireAdmin();

        return app;
    }
}
