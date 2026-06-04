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

        g.MapPost("/clone-official", async (HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(new CloneOfficialCurriculumCommand());
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapPut("/{id:guid}/hours", async (Guid id, HttpContext ctx, ISender sender, UpdateSubjectHoursCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
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
        });

        g.MapPost("/", async (HttpContext ctx, ISender sender, CreateSubjectCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd);
            return Results.Created($"/api/subjects/{result.Id}", result);
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateSubjectCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd with { Id = id });
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                await sender.Send(new DeleteSubjectCommand(id));
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
        });

        return app;
    }
}
