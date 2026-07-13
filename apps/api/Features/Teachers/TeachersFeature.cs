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

        g.MapPost("/", async (ISender sender, CreateTeacherCommand cmd) =>
        {
            var result = await sender.Send(cmd);
            return Results.Created($"/api/teachers/{result.Id}", result);
        }).RequireAdmin();

        g.MapPut("/{id:guid}", async (Guid id, ISender sender, UpdateTeacherCommand cmd) =>
        {
            try
            {
                var result = await sender.Send(cmd with { Id = id });
                return Results.Ok(result);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAdmin();

        g.MapPut("/{id:guid}/assignments", async (Guid id, ISender sender, UpdateTeacherAssignmentsCommand cmd) =>
        {
            try
            {
                var result = await sender.Send(cmd with { TeacherId = id });
                return Results.Ok(result);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAdmin();

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteTeacherCommand(id));
            return Results.NoContent();
        }).RequireAdmin();

        return app;
    }
}
