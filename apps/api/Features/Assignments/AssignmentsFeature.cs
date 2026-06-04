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

        g.MapPost("/", async (HttpContext ctx, ISender sender, CreateAssignmentCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd);
                return Results.Created($"/api/assignments/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            await sender.Send(new DeleteAssignmentCommand(id));
            return Results.NoContent();
        });

        return app;
    }
}
