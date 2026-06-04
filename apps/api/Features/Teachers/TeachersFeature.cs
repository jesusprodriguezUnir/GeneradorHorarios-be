using MediatR;
using HorariosEscolares.Application.Features.Teachers;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Teachers;

public static class TeacherEndpoints
{
    public static IEndpointRouteBuilder MapTeacherEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/teachers");

        g.MapGet("/", async (HttpContext ctx, ISender sender) =>
        {
            ctx.GetCurrentUserOrFail();
            var result = await sender.Send(new GetAllTeachersQuery());
            return Results.Ok(result);
        });

        g.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTeacherByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        g.MapPost("/", async (HttpContext ctx, ISender sender, CreateTeacherCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd);
            return Results.Created($"/api/teachers/{result.Id}", result);
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateTeacherCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd with { Id = id });
                return Results.Ok(result);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            await sender.Send(new DeleteTeacherCommand(id));
            return Results.NoContent();
        });

        return app;
    }
}
