using MediatR;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Application.Features.Schools;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Features.Auth;

namespace HorariosEscolares.Features.Schools;

public static class SchoolEndpoints
{
    public static IEndpointRouteBuilder MapSchoolEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/schools");

        g.MapGet("/me", async (ISender sender) =>
        {
            var result = await sender.Send(new GetSchoolQuery());
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        g.MapGet("/me/normative-check", async (ISender sender) =>
        {
            try
            {
                var result = await sender.Send(new NormativeCheckQuery());
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is HorariosEscolares.Application.Common.Exceptions.NotFoundException)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });

        g.MapPut("/me", async (HttpContext ctx, ISender sender, UpdateSchoolCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapGet("/me/cycles/{cycle}", async (int cycle, ISender sender) =>
        {
            var result = await sender.Send(new GetCycleScheduleQuery(cycle));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        g.MapPut("/me/cycles/{cycle}", async (int cycle, HttpContext ctx, ISender sender, UpdateCycleScheduleCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            var result = await sender.Send(cmd with { Cycle = cycle });
            return Results.Ok(result);
        });

        return app;
    }
}
