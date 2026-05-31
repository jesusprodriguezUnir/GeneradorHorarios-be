using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Schools;

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record SchoolDto(
    Guid Id, string Name, string Slug,
    string ScheduleType, string MorningStart, string? AfternoonStart, int SlotMinutes,
    int BreakAfterSlot, int BreakMinutes, int SlotsPerDay, int DaysPerWeek,
    IReadOnlyList<SlotDto> ComputedSlots);

public record SlotDto(int Index, string StartTime, string EndTime, bool IsBreak);

public record UpdateSchoolRequest(
    string? Name,
    string? ScheduleType,
    string? MorningStart,
    int? SlotMinutes,
    int? BreakAfterSlot,
    int? BreakMinutes,
    int? SlotsPerDay,
    int? DaysPerWeek,
    string? AfternoonStart);

// ── Helpers ───────────────────────────────────────────────────────────────────
public static class SlotCalculator
{
    /// <summary>Calcula las franjas horarias basándose en la configuración del colegio.</summary>
    public static List<SlotDto> Compute(School s, int totalSlots = 5)
    {
        var slots = new List<SlotDto>();
        var current = s.MorningStart;
        
        bool isPartida = s.ScheduleType == "partida" && s.AfternoonStart.HasValue;
        int morningSlotsLimit = isPartida ? Math.Min(s.BreakAfterSlot + 1, totalSlots) : totalSlots;

        // Slots de mañana
        for (int i = 0; i < morningSlotsLimit; i++)
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

        // Slots de tarde
        if (isPartida && morningSlotsLimit < totalSlots)
        {
            var afternoonCurrent = s.AfternoonStart!.Value;
            for (int i = morningSlotsLimit; i < totalSlots; i++)
            {
                var end = afternoonCurrent.AddMinutes(s.SlotMinutes);
                slots.Add(new SlotDto(i, afternoonCurrent.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false));
                afternoonCurrent = end;
            }
        }

        return slots;
    }

    public static SchoolDto ToDto(School s)
    {
        var slots = Compute(s, s.SlotsPerDay);
        return new SchoolDto(s.Id, s.Name, s.Slug, s.ScheduleType,
            s.MorningStart.ToString("HH:mm"), s.AfternoonStart?.ToString("HH:mm"), s.SlotMinutes,
            s.BreakAfterSlot, s.BreakMinutes, s.SlotsPerDay, s.DaysPerWeek, slots);
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

            // Validaciones
            if (req.SlotsPerDay.HasValue && (req.SlotsPerDay.Value < 1 || req.SlotsPerDay.Value > 10))
                return Results.BadRequest(new { message = "El número de slots por día debe estar entre 1 y 10." });

            if (req.DaysPerWeek.HasValue && (req.DaysPerWeek.Value < 1 || req.DaysPerWeek.Value > 7))
                return Results.BadRequest(new { message = "El número de días por semana debe estar entre 1 y 7." });

            var scheduleType = req.ScheduleType ?? s.ScheduleType;
            var morningStart = s.MorningStart;
            if (req.MorningStart is not null)
            {
                if (!TimeOnly.TryParse(req.MorningStart, out morningStart))
                    return Results.BadRequest(new { message = "El formato de la hora de inicio de mañana no es válido." });
            }

            TimeOnly? afternoonStart = s.AfternoonStart;
            if (req.AfternoonStart is not null)
            {
                if (TimeOnly.TryParse(req.AfternoonStart, out var parsedAfternoon))
                    afternoonStart = parsedAfternoon;
                else if (string.IsNullOrWhiteSpace(req.AfternoonStart))
                    afternoonStart = null;
                else
                    return Results.BadRequest(new { message = "El formato de la hora de inicio de tarde no es válido." });
            }

            if (scheduleType == "partida")
            {
                if (!afternoonStart.HasValue)
                    return Results.BadRequest(new { message = "Para la jornada partida es obligatorio configurar la hora de inicio de la tarde." });

                // Calcular el final de la jornada de mañana para validar que no haya solapamiento
                var slotsPerDay = req.SlotsPerDay ?? s.SlotsPerDay;
                var breakAfterSlot = req.BreakAfterSlot ?? s.BreakAfterSlot;
                var breakMinutes = req.BreakMinutes ?? s.BreakMinutes;
                var slotMinutes = req.SlotMinutes ?? s.SlotMinutes;

                int morningSlotsLimit = Math.Min(breakAfterSlot + 1, slotsPerDay);
                var morningDurationMinutes = morningSlotsLimit * slotMinutes;
                if (breakAfterSlot < morningSlotsLimit)
                {
                    morningDurationMinutes += breakMinutes;
                }
                var morningEndTime = morningStart.AddMinutes(morningDurationMinutes);

                if (afternoonStart.Value <= morningEndTime)
                    return Results.BadRequest(new { message = $"La hora de inicio de tarde ({afternoonStart.Value:HH:mm}) debe ser posterior al final de la jornada de mañana ({morningEndTime:HH:mm})." });
            }

            // Aplicar cambios
            if (req.Name is not null)           s.Name           = req.Name;
            s.ScheduleType = scheduleType;
            if (req.SlotMinutes.HasValue)        s.SlotMinutes    = req.SlotMinutes.Value;
            if (req.BreakAfterSlot.HasValue)     s.BreakAfterSlot = req.BreakAfterSlot.Value;
            if (req.BreakMinutes.HasValue)       s.BreakMinutes   = req.BreakMinutes.Value;
            s.MorningStart = morningStart;
            s.AfternoonStart = afternoonStart;
            if (req.SlotsPerDay.HasValue)       s.SlotsPerDay    = req.SlotsPerDay.Value;
            if (req.DaysPerWeek.HasValue)       s.DaysPerWeek    = req.DaysPerWeek.Value;

            await db.SaveChangesAsync();
            return Results.Ok(SlotCalculator.ToDto(s));
        });

        return app;
    }
}
