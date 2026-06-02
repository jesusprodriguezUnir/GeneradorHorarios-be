using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
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
    IReadOnlyList<SlotDto> ComputedSlots,
    // Ciclos
    IReadOnlyList<CycleScheduleDto> Cycles);

public record SlotDto(int Index, string StartTime, string EndTime, bool IsBreak);

/// <summary>Configuración de jornada (entrada/salida) de un ciclo educativo.</summary>
public record CycleScheduleDto(
    int Cycle,
    string MorningStart,
    string EndTime,
    string? AfternoonStart,
    IReadOnlyList<SlotDto> ComputedSlots);

public record UpdateCycleScheduleRequest(
    string MorningStart,
    string EndTime,
    string? AfternoonStart);

/// <summary>DTO de respuesta del endpoint GET /api/schools/me/normative-check.</summary>
public record NormativeCheckDto(
    bool IsCompliant,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<NormativeIssueDto> Issues);

public record NormativeIssueDto(
    string Severity,
    string Description,
    IReadOnlyList<string> Suggestions,
    Guid? GroupId);

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
    /// <summary>
    /// Calcula los slots lectivos para un colegio con una hora de entrada concreta.
    /// Permite pasar la entrada de mañana y tarde de un ciclo en lugar de las del colegio.
    /// </summary>
    public static List<SlotDto> Compute(
        School s,
        int totalSlots,
        TimeOnly morningStart,
        TimeOnly? afternoonStart = null)
    {
        var slots = new List<SlotDto>();
        var current = morningStart;

        bool isPartida = s.ScheduleType == "partida" && afternoonStart.HasValue && s.AfternoonSlots > 0;
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
            var afternoonCurrent = afternoonStart!.Value;
            for (int i = morningSlotsLimit; i < totalSlots; i++)
            {
                var end = afternoonCurrent.AddMinutes(s.SlotMinutes);
                slots.Add(new SlotDto(i, afternoonCurrent.ToString("HH:mm"), end.ToString("HH:mm"), IsBreak: false));
                afternoonCurrent = end;
            }
        }

        return slots;
    }

    /// <summary>Sobrecarga de compatibilidad: usa los campos de entrada del propio colegio.</summary>
    public static List<SlotDto> Compute(School s, int totalSlots = 5)
        => Compute(s, totalSlots, s.MorningStart, s.AfternoonStart);

    /// <summary>
    /// Calcula la hora de fin de jornada a partir de una entrada de mañana y los
    /// parámetros globales del colegio (slots, duración, recreo).
    /// </summary>
    public static TimeOnly ComputeEndTime(School s, TimeOnly morningStart, TimeOnly? afternoonStart = null)
    {
        var current = morningStart;
        var finalAfternoonStart = afternoonStart ?? s.AfternoonStart;
        bool isPartida = s.ScheduleType == "partida" && finalAfternoonStart.HasValue && s.AfternoonSlots > 0;
        int morningSlots = isPartida ? s.SlotsPerDay - s.AfternoonSlots : s.SlotsPerDay;

        // Avanzar slot a slot incluyendo el recreo si cae dentro de la mañana
        for (int i = 0; i < morningSlots; i++)
        {
            if (i == s.BreakAfterSlot)
                current = current.AddMinutes(s.BreakMinutes);
            current = current.AddMinutes(s.SlotMinutes);
        }

        if (isPartida)
        {
            // En jornada partida la salida es al final de la tarde
            var afternoonCurrent = finalAfternoonStart!.Value;
            for (int i = morningSlots; i < s.SlotsPerDay; i++)
                afternoonCurrent = afternoonCurrent.AddMinutes(s.SlotMinutes);
            return afternoonCurrent;
        }

        return current;
    }

    public static SchoolDto ToDto(School s, IEnumerable<CycleSchedule> cycles)
    {
        var slots = Compute(s, s.SlotsPerDay);
        var workingDays = ParseWorkingDays(s.WorkingDays);
        var cycleDtos = cycles
            .OrderBy(c => c.Cycle)
            .Select(c => ToCycleDto(s, c))
            .ToList();

        return new SchoolDto(
            s.Id, s.Name, s.Slug,
            s.CenterCode, s.Locality, s.Community,
            s.Stage, s.MinCourseLevel, s.MaxCourseLevel, s.AcademicYear,
            s.ScheduleType,
            s.MorningStart.ToString("HH:mm"), s.AfternoonStart?.ToString("HH:mm"),
            s.SlotMinutes, s.BreakAfterSlot, s.BreakMinutes,
            s.SlotsPerDay, s.AfternoonSlots, s.DaysPerWeek,
            workingDays, slots,
            cycleDtos);
    }

    /// <summary>Sobrecarga sin ciclos para contextos donde no se han cargado (compat).</summary>
    public static SchoolDto ToDto(School s)
        => ToDto(s, []);

    public static CycleScheduleDto ToCycleDto(School s, CycleSchedule c)
    {
        var slots = Compute(s, s.SlotsPerDay, c.MorningStart, c.AfternoonStart);
        return new CycleScheduleDto(
            c.Cycle,
            c.MorningStart.ToString("HH:mm"),
            c.EndTime.ToString("HH:mm"),
            c.AfternoonStart?.ToString("HH:mm"),
            slots);
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
            if (s is null) return Results.NotFound();
            var cycles = await db.CycleSchedules.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId).ToListAsync();
            return Results.Ok(SlotCalculator.ToDto(s, cycles));
        });

        // GET /api/schools/me/normative-check — validación normativa (Decreto 61/2022 Madrid)
        g.MapGet("/me/normative-check", async (
            HttpContext ctx, AppDbContext db, INormativeValidator validator) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var school = await db.Schools.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == user.SchoolId);
            if (school is null) return Results.NotFound();

            // Cargar todas las asignaciones del colegio con su SubjectAllocation
            var assignments = await db.Assignments.AsNoTracking()
                .Where(a => a.SchoolId == user.SchoolId)
                .ToListAsync();
            var allocations = await db.SubjectAllocations.AsNoTracking()
                .ToDictionaryAsync(a => a.Id);

            var assignmentData = assignments
                .Where(a => allocations.ContainsKey(a.AllocationId))
                .Select(a => (a, allocations[a.AllocationId]))
                .ToList();

            var issues = await validator.ValidateAsync(school, assignmentData);

            var dto = new NormativeCheckDto(
                IsCompliant  : !issues.Any(i => i.Severity == ConflictSeverity.Error),
                ErrorCount   : issues.Count(i => i.Severity == ConflictSeverity.Error),
                WarningCount : issues.Count(i => i.Severity == ConflictSeverity.Warning),
                Issues       : issues.Select(i => new NormativeIssueDto(
                    Severity    : i.Severity.ToString().ToLower(),
                    Description : i.Description,
                    Suggestions : i.Suggestions,
                    GroupId     : i.GroupId)).ToList());

            return Results.Ok(dto);
        });

        // PUT /api/schools/me — solo admin
        g.MapPut("/me", async (HttpContext ctx, AppDbContext db, UpdateSchoolRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

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

            var cycles = await db.CycleSchedules.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId).ToListAsync();
            return Results.Ok(SlotCalculator.ToDto(s, cycles));
        });

        // PUT /api/schools/me/cycles/{cycle} — actualiza la jornada de un ciclo (solo admin)
        g.MapPut("/me/cycles/{cycle:int}", async (
            int cycle,
            HttpContext ctx,
            AppDbContext db,
            UpdateCycleScheduleRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);
            if (cycle < 1 || cycle > 3)
                return Results.BadRequest(new { message = "El ciclo debe ser 1, 2 o 3." });

            var school = await db.Schools.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == user.SchoolId);
            if (school is null) return Results.NotFound();

            // ── Parseo de horas ────────────────────────────────────────────────
            if (!TimeOnly.TryParse(req.MorningStart, out var morningStart))
                return Results.BadRequest(new { message = "Formato de hora de entrada no válido." });
            if (!TimeOnly.TryParse(req.EndTime, out var endTime))
                return Results.BadRequest(new { message = "Formato de hora de salida no válido." });

            TimeOnly? afternoonStart = null;
            if (req.AfternoonStart is not null)
            {
                if (!TimeOnly.TryParse(req.AfternoonStart, out var parsedAfternoon))
                    return Results.BadRequest(new { message = "Formato de hora de inicio de tarde no válido." });
                afternoonStart = parsedAfternoon;
            }

            // ── Validación: la salida debe coincidir con la calculada ─────────
            var calculatedEnd = SlotCalculator.ComputeEndTime(school, morningStart, afternoonStart);
            // Tolerancia de ±1 minuto para no penalizar redondeos de la UI
            var diffMinutes = Math.Abs((endTime - calculatedEnd).TotalMinutes);
            if (diffMinutes > 1)
                return Results.BadRequest(new
                {
                    message = $"La hora de salida ({endTime:HH:mm}) no coincide con la calculada " +
                              $"a partir de la entrada y los parámetros de jornada del centro ({calculatedEnd:HH:mm}). " +
                              $"Ajusta la entrada o los parámetros de jornada global."
                });

            // ── Upsert ────────────────────────────────────────────────────────
            var existing = await db.CycleSchedules
                .FirstOrDefaultAsync(c => c.SchoolId == user.SchoolId && c.Cycle == cycle);

            if (existing is null)
            {
                existing = new CycleSchedule
                {
                    SchoolId = user.SchoolId,
                    Cycle = cycle,
                };
                db.CycleSchedules.Add(existing);
            }

            existing.MorningStart   = morningStart;
            existing.EndTime        = endTime;
            existing.AfternoonStart = afternoonStart;

            await db.SaveChangesAsync();

            return Results.Ok(SlotCalculator.ToCycleDto(school, existing));
        });

        return app;
    }
}
