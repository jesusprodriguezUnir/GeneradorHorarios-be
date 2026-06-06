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

        // ── School (compatibilidad) ─────────────────────────────────────────
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

        // ── Cycles (compatibilidad — apunta al periodo por defecto) ─────────
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

        // ── Stages (etapas / bloques) CRUD ──────────────────────────────────
        g.MapGet("/me/stages", async (ISender sender) =>
        {
            var result = await sender.Send(new GetStagesQuery());
            return Results.Ok(result);
        });

        g.MapPost("/me/stages", async (HttpContext ctx, ISender sender, CreateStageCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd);
                return Results.Created($"/api/schools/me/stages/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapDelete("/me/stages/{stageId:guid}", async (Guid stageId, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                await sender.Send(new DeleteStageCommand(stageId));
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapPut("/me/stages/{stageId:guid}", async (Guid stageId, HttpContext ctx, ISender sender, UpdateStageCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd with { StageId = stageId });
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // ── Periods CRUD ────────────────────────────────────────────────────
        g.MapGet("/me/periods", async (ISender sender, Guid? stageId) =>
        {
            var result = await sender.Send(new GetPeriodsQuery(stageId));
            return Results.Ok(result);
        });

        g.MapGet("/me/periods/{periodId:guid}", async (Guid periodId, ISender sender) =>
        {
            var result = await sender.Send(new GetPeriodQuery(periodId));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        g.MapPost("/me/periods", async (HttpContext ctx, ISender sender, CreatePeriodCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd);
                return Results.Created($"/api/schools/me/periods/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapPut("/me/periods/{periodId:guid}", async (Guid periodId, HttpContext ctx, ISender sender, UpdatePeriodCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd with { PeriodId = periodId });
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        g.MapDelete("/me/periods/{periodId:guid}", async (Guid periodId, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                await sender.Send(new DeletePeriodCommand(periodId));
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // ── Period cycles ───────────────────────────────────────────────────
        g.MapGet("/me/periods/{periodId:guid}/cycles/{cycle}", async (Guid periodId, int cycle, ISender sender) =>
        {
            var result = await sender.Send(new GetPeriodCycleQuery(periodId, cycle));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        g.MapPut("/me/periods/{periodId:guid}/cycles/{cycle}", async (Guid periodId, int cycle, HttpContext ctx, ISender sender, UpdatePeriodCycleCommand cmd) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var result = await sender.Send(cmd with { PeriodId = periodId, Cycle = cycle });
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }
}
