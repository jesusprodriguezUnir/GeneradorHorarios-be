using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Schools;

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record SchoolDto(
    Guid Id, string Name, string Slug,
    string ScheduleType, string MorningStart, int SlotMinutes,
    int BreakAfterSlot, int BreakMinutes,
    IReadOnlyList<SlotDto> ComputedSlots);

public record SlotDto(int Index, string StartTime, string EndTime, bool IsBreak);

public record UpdateSchoolRequest(
    string? Name,
    string? ScheduleType,
    string? MorningStart,
    int? SlotMinutes,
    int? BreakAfterSlot,
    int? BreakMinutes);

// ── Helpers ───────────────────────────────────────────────────────────────────
public static class SlotCalculator
{
    /// <summary>Calcula las franjas horarias basándose en la configuración del colegio.</summary>
    public static List<SlotDto> Compute(School s, int totalSlots = 5)
    {
        var slots = new List<SlotDto>();
        var current = s.MorningStart;
        for (int i = 0; i < totalSlots; i++)
        {
            if (i == s.BreakAfterSlot)
            {
                // Insertar recreo
                slots.Add(new SlotDto(-1,
                    current.ToString("HH:mm"),
                    current.AddMinutes(s.BreakMinutes).ToString("HH:mm"),
                    IsBreak: true));
                current = current.AddMinutes(s.BreakMinutes);
            }
            var end = current.AddMinutes(s.SlotMinutes);
            slots.Add(new SlotDto(i, current.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false));
            current = end;
        }
        return slots;
    }

    public static SchoolDto ToDto(School s)
    {
        var slots = Compute(s, s.SlotsPerDay);
        return new SchoolDto(s.Id, s.Name, s.Slug, s.ScheduleType,
            s.MorningStart.ToString("HH:mm"), s.SlotMinutes,
            s.BreakAfterSlot, s.BreakMinutes, slots);
    }
}

// ── Endpoints ─────────────────────────────────────────────────────────────────
public static class SchoolEndpoints
{
    public static IEndpointRouteBuilder MapSchoolEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/schools");

        // GET /api/schools/me
        g.MapGet("/me", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var s = await db.Schools.AsNoTracking().FirstOrDefaultAsync(x => x.Id == user.SchoolId);
            return s is null ? Results.NotFound() : Results.Ok(SlotCalculator.ToDto(s));
        });

        // PUT /api/schools/me — solo admin
        g.MapPut("/me", async (HttpContext ctx, AppDbContext db, UpdateSchoolRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.Forbid();

            var s = await db.Schools.FirstOrDefaultAsync(x => x.Id == user.SchoolId);
            if (s is null) return Results.NotFound();

            if (req.Name is not null)           s.Name           = req.Name;
            if (req.ScheduleType is not null)   s.ScheduleType   = req.ScheduleType;
            if (req.SlotMinutes.HasValue)        s.SlotMinutes    = req.SlotMinutes.Value;
            if (req.BreakAfterSlot.HasValue)     s.BreakAfterSlot = req.BreakAfterSlot.Value;
            if (req.BreakMinutes.HasValue)       s.BreakMinutes   = req.BreakMinutes.Value;
            if (req.MorningStart is not null &&
                TimeOnly.TryParse(req.MorningStart, out var t)) s.MorningStart = t;

            await db.SaveChangesAsync();
            return Results.Ok(SlotCalculator.ToDto(s));
        });

        return app;
    }
}
