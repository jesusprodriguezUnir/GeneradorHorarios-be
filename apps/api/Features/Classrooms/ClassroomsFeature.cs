using MediatR;
using HorariosEscolares.Application.Features.Classrooms;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Classrooms;

public static class ClassroomEndpoints
{
    public static IEndpointRouteBuilder MapClassroomEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/classrooms");

        g.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAllClassroomsQuery());
            return Results.Ok(result);
        });

        g.MapPost("/", async (HttpContext ctx, ISender sender, CreateClassroomCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd);
            return Results.Created($"/api/classrooms/{result.Id}", result);
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateClassroomCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd with { Id = id });
            return Results.Ok(result);
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            await sender.Send(new DeleteClassroomCommand(id));
            return Results.NoContent();
        });

        return app;
    }
}
