using MediatR;
using HorariosEscolares.Application.Features.Assignments;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Assignments;

public static class AssignmentEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/assignments");

        g.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAllAssignmentsQuery());
            return Results.Ok(result);
        });

        g.MapPost("/", async (ISender sender, CreateAssignmentCommand cmd) =>
        {
            try
            {
                var result = await sender.Send(cmd);
                return Results.Created($"/api/assignments/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAdmin();

        g.MapPut("/", async (ISender sender, UpdateAssignmentsCommand cmd) =>
        {
            try
            {
                await sender.Send(cmd);
                return Results.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAdmin();

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteAssignmentCommand(id));
            return Results.NoContent();
        }).RequireAdmin();

        return app;
    }
}
