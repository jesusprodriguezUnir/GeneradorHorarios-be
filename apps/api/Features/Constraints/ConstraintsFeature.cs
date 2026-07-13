using MediatR;
using HorariosEscolares.Application.Features.Constraints;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Constraints;

public static class ConstraintEndpoints
{
    public static IEndpointRouteBuilder MapConstraintEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/constraints");

        g.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAllConstraintsQuery());
            return Results.Ok(result);
        });

        g.MapGet("/teacher/{teacherId:guid}", async (Guid teacherId, ISender sender) =>
        {
            var result = await sender.Send(new GetConstraintsByTeacherQuery(teacherId));
            return Results.Ok(result);
        });

        g.MapPost("/", async (ISender sender, CreateConstraintCommand cmd) =>
        {
            var result = await sender.Send(cmd);
            return Results.Created($"/api/constraints/{result.Id}", result);
        }).RequireAdmin();

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteConstraintCommand(id));
            return Results.NoContent();
        }).RequireAdmin();

        return app;
    }
}
