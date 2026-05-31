using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Features.Schools;

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record SchoolDto(
    Guid Id, string Name, string Slug,
    // Identificación
    string? CenterCode, string? Locality, string Community,
    string Stage, int MinCourseLevel, int MaxCourseLevel, string AcademicYear,
    // Jornada
    string ScheduleType, string MorningStart, string? AfternoonStart,
    int SlotMinutes, int BreakAfterSlot, int BreakMinutes,
    int SlotsPerDay, int AfternoonSlots, int DaysPerWeek,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<SlotDto> ComputedSlots);

public record SlotDto(int Index, string StartTime, string EndTime, bool IsBreak);

public record UpdateSchoolRequest(
    string? Name,
    // Identificación
    string? CenterCode,
    string? Locality,
    string? Community,
    string? Stage,
    int? MinCourseLevel,
    int? MaxCourseLevel,
    string? AcademicYear,
    // Jornada
    string? ScheduleType,
    string? MorningStart,
    int? SlotMinutes,
    int? BreakAfterSlot,
    int? BreakMinutes,
    int? SlotsPerDay,
    int? AfternoonSlots,
    string? AfternoonStart,
    IReadOnlyList<int>? WorkingDays);

// ── Helpers ───────────────────────────────────────────────────────────────────
public static class SlotCalculator
{
    public static List<SlotDto> Compute(School s, int totalSlots = 5)
    {
        var slots = new List<SlotDto>();
        var current = s.MorningStart;

        bool isPartida = s.ScheduleType == "partida" && s.AfternoonStart.HasValue && s.AfternoonSlots > 0;
        int morningSlotsLimit = isPartida ? totalSlots - s.AfternoonSlots : totalSlots;

        // Slots de mañana
        for (int i = 0; i < morningSlotsLimit; i++)
        {
            if (i == s.BreakAfterSlot)
            {
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

        // Slots de tarde (solo jornada partida)
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
        var workingDays = ParseWorkingDays(s.WorkingDays);
        return new SchoolDto(
            s.Id, s.Name, s.Slug,
            s.CenterCode, s.Locality, s.Community,
            s.Stage, s.MinCourseLevel, s.MaxCourseLevel, s.AcademicYear,
            s.ScheduleType,
            s.MorningStart.ToString("HH:mm"), s.AfternoonStart?.ToString("HH:mm"),
            s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.SlotsPerDay, s.AfternoonSlots, s.DaysPerWeek,
            workingDays, slots);
    }

    public static IReadOnlyList<int> ParseWorkingDays(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? [1, 2, 3, 4, 5];
        }
        catch
        {
            return [1, 2, 3, 4, 5];
        }
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

            // ── Validaciones de jornada ────────────────────────────────────────
            var slotsPerDay = req.SlotsPerDay ?? s.SlotsPerDay;
            if (slotsPerDay < 1 || slotsPerDay > 10)
                return Results.BadRequest(new { message = "El número de slots por día debe estar entre 1 y 10." });

            var afternoonSlots = req.AfternoonSlots ?? s.AfternoonSlots;
            if (afternoonSlots < 0 || afternoonSlots >= slotsPerDay)
                return Results.BadRequest(new { message = "Los slots de tarde deben estar entre 0 y slotsPerDay-1." });

            // ── Validación días lectivos ───────────────────────────────────────
            IReadOnlyList<int> workingDays = s.WorkingDays != null
                ? SlotCalculator.ParseWorkingDays(s.WorkingDays)
                : [1, 2, 3, 4, 5];

            if (req.WorkingDays is not null)
            {
                if (req.WorkingDays.Count == 0)
                    return Results.BadRequest(new { message = "Debe haber al menos un día lectivo." });
                if (req.WorkingDays.Any(d => d < 1 || d > 7))
                    return Results.BadRequest(new { message = "Los días lectivos deben estar entre 1 (lunes) y 7 (domingo)." });
                if (req.WorkingDays.Distinct().Count() != req.WorkingDays.Count)
                    return Results.BadRequest(new { message = "Los días lectivos no pueden repetirse." });
                workingDays = req.WorkingDays;
            }

            // ── Validaciones de identificación ────────────────────────────────
            var minLevel = req.MinCourseLevel ?? s.MinCourseLevel;
            var maxLevel = req.MaxCourseLevel ?? s.MaxCourseLevel;
            if (minLevel < 1 || maxLevel > 12 || minLevel > maxLevel)
                return Results.BadRequest(new { message = "El rango de cursos no es válido (minCourseLevel ≤ maxCourseLevel, entre 1 y 12)." });

            // ── Parseo de horas ────────────────────────────────────────────────
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

            // ── Validación jornada partida ─────────────────────────────────────
            if (scheduleType == "partida")
            {
                if (!afternoonStart.HasValue)
                    return Results.BadRequest(new { message = "Para la jornada partida es obligatorio configurar la hora de inicio de la tarde." });

                var breakAfterSlot = req.BreakAfterSlot ?? s.BreakAfterSlot;
                var breakMinutes = req.BreakMinutes ?? s.BreakMinutes;
                var slotMinutes = req.SlotMinutes ?? s.SlotMinutes;
                int morningSlotsLimit = slotsPerDay - afternoonSlots;

                var morningDurationMinutes = morningSlotsLimit * slotMinutes;
                if (breakAfterSlot < morningSlotsLimit)
                    morningDurationMinutes += breakMinutes;

                var morningEndTime = morningStart.AddMinutes(morningDurationMinutes);
                if (afternoonStart.Value <= morningEndTime)
                    return Results.BadRequest(new { message = $"La hora de inicio de tarde ({afternoonStart.Value:HH:mm}) debe ser posterior al final de la jornada de mañana ({morningEndTime:HH:mm})." });
            }

            // ── Aplicar cambios ────────────────────────────────────────────────
            if (req.Name is not null)             s.Name             = req.Name;
            if (req.CenterCode is not null)       s.CenterCode       = string.IsNullOrWhiteSpace(req.CenterCode) ? null : req.CenterCode;
            if (req.Locality is not null)         s.Locality         = string.IsNullOrWhiteSpace(req.Locality) ? null : req.Locality;
            if (req.Community is not null)        s.Community        = req.Community;
            if (req.Stage is not null)            s.Stage            = req.Stage;
            s.MinCourseLevel  = minLevel;
            s.MaxCourseLevel  = maxLevel;
            if (req.AcademicYear is not null)     s.AcademicYear     = req.AcademicYear;
            s.ScheduleType    = scheduleType;
            if (req.SlotMinutes.HasValue)         s.SlotMinutes      = req.SlotMinutes.Value;
            if (req.BreakAfterSlot.HasValue)      s.BreakAfterSlot   = req.BreakAfterSlot.Value;
            if (req.BreakMinutes.HasValue)        s.BreakMinutes     = req.BreakMinutes.Value;
            s.MorningStart    = morningStart;
            s.AfternoonStart  = afternoonStart;
            s.SlotsPerDay     = slotsPerDay;
            s.AfternoonSlots  = afternoonSlots;
            s.WorkingDays     = JsonSerializer.Serialize(workingDays.OrderBy(d => d).ToList());
            s.DaysPerWeek     = workingDays.Count;   // derivado de WorkingDays

            await db.SaveChangesAsync();
            return Results.Ok(SlotCalculator.ToDto(s));
        });

        return app;
    }
}
