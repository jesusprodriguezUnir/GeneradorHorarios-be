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

        g.MapPost("/", async (ISender sender, CreateClassroomCommand cmd) =>
        {
            var result = await sender.Send(cmd);
            return Results.Created($"/api/classrooms/{result.Id}", result);
        }).RequireAdmin();

        g.MapPut("/{id:guid}", async (Guid id, ISender sender, UpdateClassroomCommand cmd) =>
        {
            var result = await sender.Send(cmd with { Id = id });
            return Results.Ok(result);
        }).RequireAdmin();

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteClassroomCommand(id));
            return Results.NoContent();
        }).RequireAdmin();

        return app;
    }
}
