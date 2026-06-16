using MediatR;
using HorariosEscolares.Application.Features.Groups;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Groups;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/groups");

        g.MapGet("/", async (ISender sender, Guid? stageId) =>
        {
            var result = await sender.Send(new GetAllGroupsQuery(stageId));
            return Results.Ok(result);
        });

        g.MapPost("/", async (ISender sender, CreateGroupCommand cmd) =>
        {
            var result = await sender.Send(cmd);
            return Results.Created($"/api/groups/{result.Id}", result);
        }).RequireAdmin();

        g.MapPut("/{id:guid}", async (Guid id, ISender sender, UpdateGroupCommand cmd) =>
        {
            var result = await sender.Send(cmd with { Id = id });
            return Results.Ok(result);
        }).RequireAdmin();

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteGroupCommand(id));
            return Results.NoContent();
        }).RequireAdmin();

        return app;
    }
}
