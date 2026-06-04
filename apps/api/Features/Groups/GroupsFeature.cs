using MediatR;
using HorariosEscolares.Application.Features.Groups;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Groups;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/groups");

        g.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAllGroupsQuery());
            return Results.Ok(result);
        });

        g.MapPost("/", async (HttpContext ctx, ISender sender, CreateGroupCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd);
            return Results.Created($"/api/groups/{result.Id}", result);
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateGroupCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd with { Id = id });
            return Results.Ok(result);
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            await sender.Send(new DeleteGroupCommand(id));
            return Results.NoContent();
        });

        return app;
    }
}
