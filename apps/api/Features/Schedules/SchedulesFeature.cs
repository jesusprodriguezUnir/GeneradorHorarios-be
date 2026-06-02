using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Features.Schools;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;
// Alias para desambiguar entidades con el mismo nombre en Domain y Persistence
using DbScheduleEntry = HorariosEscolares.Infrastructure.Persistence.Entities.ScheduleEntry;
using DbScheduleConflict = HorariosEscolares.Infrastructure.Persistence.Entities.ScheduleConflictRecord;

namespace HorariosEscolares.Features.Schedules;

// ── SignalR Hub ───────────────────────────────────────────────────────────────
public sealed class GenerationProgressHub : Hub
{
    public async Task JoinSchoolGroup(string schoolId)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null) throw new HubException("No autorizado.");

        var user = httpContext.GetCurrentUserOrFail();
        if (user.SchoolId.ToString() != schoolId)
            throw new HubException("No autorizado para este colegio.");

        await Groups.AddToGroupAsync(Context.ConnectionId, schoolId);
    }

    public async Task LeaveSchoolGroup(string schoolId)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null) throw new HubException("No autorizado.");

        var user = httpContext.GetCurrentUserOrFail();
        if (user.SchoolId.ToString() != schoolId)
            throw new HubException("No autorizado para este colegio.");

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, schoolId);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record ScheduleListDto(
    Guid Id, string AcademicYear, string Status,
    DateTime? GeneratedAt, DateTime? PublishedAt,
    int TotalConflicts, int? GenerationSeconds);

public record ConflictDto(
    string Type, string Severity, string Description, string[] Suggestions,
    Guid? GroupId, Guid? TeacherId, int? DayOfWeek, int? SlotIndex);

public record ScheduleGridDto(
    Guid ScheduleId, string Status, string AcademicYear,
    IReadOnlyList<ScheduleGridEntry> Entries,
    IReadOnlyList<ConflictDto> Conflicts,
    IReadOnlyList<SlotInfo> Slots,
    IReadOnlyDictionary<int, IReadOnlyList<SlotInfo>> SlotsByCycle);

public record ScheduleGridEntry(
    Guid Id, int DayOfWeek, int SlotIndex,
    Guid GroupId, string GroupDisplay,
    Guid TeacherId, string TeacherName, string TeacherColorKey,
    Guid AllocationId, string SubjectName, string SubjectKey, string SubjectShort,
    Guid ClassroomId, string ClassroomName,
    bool IsManualOverride);

public record SlotInfo(int Index, string StartTime, string EndTime, bool IsBreak);

public record MyScheduleDto(
    string TeacherName, string SchoolName, string AcademicYear,
    IReadOnlyList<MyScheduleEntry> Entries,
    IReadOnlyList<SlotInfo> Slots);

public record MyScheduleEntry(
    int DayOfWeek, int SlotIndex, string SlotTime,
    string SubjectName, string SubjectKey, string SubjectShort,
    string GroupLabel, string ClassroomName);

public record GenerateRequest(string AcademicYear, int TimeoutSeconds = 30);
public record UpdateEntryRequest(Guid TeacherId, Guid ClassroomId);

// ── Endpoints ─────────────────────────────────────────────────────────────────
public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/schedules");

        // GET /api/schedules — lista de horarios del colegio
        g.MapGet("/", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var list = await db.Schedules.AsNoTracking()
                .Where(s => s.SchoolId == user.SchoolId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new ScheduleListDto(s.Id, s.AcademicYear, s.Status,
                    s.GeneratedAt, s.PublishedAt, s.TotalConflicts, s.GenerationSeconds))
                .ToListAsync();
            return Results.Ok(list);
        });

        // POST /api/schedules/generate — motor de backtracking + SignalR
        g.MapPost("/generate", async (
            HttpContext ctx, AppDbContext db,
            IScheduleEngine engine,
            INormativeValidator normativeValidator,
            IHubContext<GenerationProgressHub> hub,
            GenerateRequest req,
            CancellationToken ct) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var school = await db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.SchoolId, ct);
            if (school is null) return Results.NotFound(new { message = "Colegio no encontrado." });

            // ── Construir contexto del motor desde la BD ──────────────────────
            var sessions = await BuildSessions(db, user.SchoolId, ct);
            if (sessions.Count == 0)
                return Results.BadRequest(new { message = "No hay asignaciones configuradas. Ve a Configuración → Asignaturas para asignar profesores a grupos." });

            var classrooms = await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId)
                .ToListAsync(ct);

            // Cargar slots de no-disponibilidad (compartido por HardConstraints y ViabilityAnalyzer)
            var unavailableSlots = await db.TeacherConstraints.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId && c.ConstraintType == "unavailable")
                .Select(c => new { c.TeacherId, Day = c.DayOfWeek, Slot = c.SlotIndex })
                .ToListAsync(ct);
            var unavailableSet = unavailableSlots
                .Select(c => (c.TeacherId, c.Day, c.Slot))
                .ToHashSet();

            // ── Validación normativa (Decreto 61/2022) — no bloqueante ────────
            // Se ejecuta antes de generar para adjuntar incidencias legales al resultado.
            var normativeAllocs = await db.SubjectAllocations.AsNoTracking()
                .ToDictionaryAsync(a => a.Id, ct);
            var normativeAssignments = await db.Assignments.AsNoTracking()
                .Where(a => a.SchoolId == user.SchoolId).ToListAsync(ct);
            var normativeData = normativeAssignments
                .Where(a => normativeAllocs.ContainsKey(a.AllocationId))
                .Select(a => (a, normativeAllocs[a.AllocationId]))
                .ToList();
            var normativeIssues = await normativeValidator.ValidateAsync(school, normativeData, ct);

            var slots = SlotCalculator.Compute(school, school.SlotsPerDay);
            int lastLectivoSlotIndex = slots.Where(s => !s.IsBreak).Any()
                ? slots.Where(s => !s.IsBreak).Max(s => s.Index)
                : 4;

            var weights = new SoftConstraintWeights(); // defaults pedagógicos; en v2+ se leerán de BD por colegio
            var hardConstraints = BuildHardConstraints(unavailableSlots.Select(c => (c.TeacherId, c.Day, c.Slot)).ToList());
            var softConstraints = await BuildSoftConstraints(db, user.SchoolId, lastLectivoSlotIndex, weights, ct);

            var workingDays = SlotCalculator.ParseWorkingDays(school.WorkingDays);
            var context = new GenerationContext
            {
                School = new SchoolConfig(
                    school.SlotsPerDay,
                    school.DaysPerWeek,
                    workingDays,
                    slots.Select(s => new SlotConfig(s.Index, s.IsBreak)).ToList(),
                    classrooms.Select(c => new ClassroomInfo(c.Id, c.Name, ParseClassroomType(c.ClassroomType))).ToList()
                ),
                Sessions = sessions,
                HardConstraints = hardConstraints,
                SoftConstraints = softConstraints,
                TimeoutSeconds = req.TimeoutSeconds,
                Weights = weights,
            };

            // ── Pre-flight: análisis de viabilidad (rápido, sin ejecutar el motor) ──
            var viabilityErrors = ScheduleViabilityAnalyzer.Analyze(context.School, sessions, unavailableSet);
            if (viabilityErrors.Any())
            {
                // Configuración irresoluble: persistir el intento como 'failed' y devolver
                // los conflictos sin ejecutar el backtracking (evita timeout innecesario).
                var failedSchedule = new ScheduleRecord
                {
                    SchoolId = user.SchoolId, AcademicYear = req.AcademicYear,
                    Status = "failed",
                    GeneratedAt = DateTime.UtcNow, GenerationSeconds = 0,
                    TotalConflicts = viabilityErrors.Count,
                    CreatedBy = user.UserId,
                };
                db.Schedules.Add(failedSchedule);

                foreach (var conflict in viabilityErrors)
                {
                    db.ScheduleConflicts.Add(new ScheduleConflictRecord
                    {
                        ScheduleId = failedSchedule.Id,
                        ConflictType = conflict.Type.ToString().ToLower(),
                        Severity = conflict.Severity.ToString().ToLower(),
                        Description = conflict.Description,
                        Suggestions = JsonSerializer.Serialize(conflict.Suggestions),
                        GroupId = conflict.GroupId, TeacherId = conflict.TeacherId,
                        DayOfWeek = conflict.DayOfWeek, SlotIndex = conflict.SlotIndex,
                    });
                }
                await db.SaveChangesAsync(ct);

                return Results.UnprocessableEntity(new
                {
                    scheduleId = failedSchedule.Id,
                    status = "failed",
                    totalAssigned = 0,
                    totalRequired = sessions.Count,
                    elapsedSeconds = 0,
                    totalConflicts = viabilityErrors.Count,
                    conflicts = viabilityErrors.Select(c => new
                    {
                        type = c.Type.ToString().ToLower(),
                        severity = c.Severity.ToString().ToLower(),
                        description = c.Description,
                        suggestions = c.Suggestions,
                        teacherId = c.TeacherId,
                        groupId = c.GroupId,
                    }),
                });
            }

            // ── Progreso via SignalR ───────────────────────────────────────────
            var progress = new Progress<GenerationProgress>(async p =>
            {
                try
                {
                    await hub.Clients.Group(user.SchoolId.ToString())
                        .SendAsync("Progress", new
                        {
                            assigned = p.Assigned,
                            total = p.Total,
                            percentage = p.Percentage,
                            currentAction = p.CurrentAction,
                        }, ct);
                }
                catch { /* SignalR errors no deben abortar la generación */ }
            });

            // ── Ejecutar motor ────────────────────────────────────────────────
            var result = await engine.GenerateAsync(context, ct, progress);

            // ── Persistir horario ─────────────────────────────────────────────
            // Pre-cargar datos para resolver aulas y detectar cobertura/horas
            var homeClassroomMap = await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId && g.HomeClassroomId.HasValue)
                .ToDictionaryAsync(g => g.Id, g => g.HomeClassroomId!.Value, ct);
            var defaultClassroomId = await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId && c.ClassroomType == "regular")
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);
            var validClassroomIds = (await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId)
                .Select(c => c.Id)
                .ToListAsync(ct)).ToHashSet();
            var allGroups = await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId)
                .ToDictionaryAsync(g => g.Id, ct);
            var teacherAssignedWeeklyHoursExpected = await db.Assignments.AsNoTracking()
                .Where(a => a.SchoolId == user.SchoolId)
                .GroupBy(a => a.TeacherId)
                .Select(g => new { TeacherId = g.Key, ExpectedHours = g.Sum(a => a.WeeklyHours) })
                .ToDictionaryAsync(x => x.TeacherId, x => x.ExpectedHours, ct);
            var teacherNames = await db.Teachers.AsNoTracking()
                .Where(t => t.SchoolId == user.SchoolId)
                .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);

            // ── Cobertura: detectar huecos vacíos por grupo ───────────────────
            var workingDaysList = SlotCalculator.ParseWorkingDays(school.WorkingDays);
            var lectivoSlotIndices = slots.Where(s => !s.IsBreak).Select(s => s.Index).ToHashSet();

            // Conjunto de (groupId, day, slotIndex) ya asignados
            var assignedSet = result.AssignedSlots
                .Select(s => (s.GroupId, s.DayOfWeek, s.SlotIndex))
                .ToHashSet();

            var coverageConflicts = new List<ConflictExplanation>();
            foreach (var (groupId, group) in allGroups)
            {
                foreach (var day in workingDaysList)
                {
                    foreach (var slotIdx in lectivoSlotIndices)
                    {
                        if (!assignedSet.Contains((groupId, day, slotIdx)))
                        {
                            coverageConflicts.Add(new ConflictExplanation
                            {
                                Type = ConflictType.Coverage,
                                Severity = ConflictSeverity.Warning,
                                Description = $"Hueco sin cubrir para {group.DisplayName} el {DayName(day)} en la hora {slotIdx + 1}.",
                                Suggestions = ["Revisa las asignaciones del grupo o amplía sus horas lectivas."],
                                GroupId = groupId,
                                DayOfWeek = day,
                                SlotIndex = slotIdx,
                            });
                        }
                    }
                }
            }

            // ── Horas de profesor: avisar si no se cubrieron todas ────────────
            var teacherActualHours = result.AssignedSlots
                .GroupBy(s => s.TeacherId)
                .ToDictionary(g => g.Key, g => g.Count());

            var teacherHoursConflicts = new List<ConflictExplanation>();
            foreach (var (teacherId, expectedHours) in teacherAssignedWeeklyHoursExpected)
            {
                var actual = teacherActualHours.GetValueOrDefault(teacherId, 0);
                if (actual < expectedHours)
                {
                    var name = teacherNames.GetValueOrDefault(teacherId, "Profesor");
                    teacherHoursConflicts.Add(new ConflictExplanation
                    {
                        Type = ConflictType.Teacher,
                        Severity = ConflictSeverity.Warning,
                        Description = $"{name} tiene {actual} horas asignadas de {expectedHours} configuradas.",
                        Suggestions = ["Comprueba que no haya conflictos de disponibilidad o aulas especiales sin cubrir."],
                        TeacherId = teacherId,
                    });
                }
            }

            var schedule = new ScheduleRecord
            {
                SchoolId = user.SchoolId, AcademicYear = req.AcademicYear,
                Status = "generated",
                GeneratedAt = DateTime.UtcNow, GenerationSeconds = result.ElapsedSeconds,
                // Incluye conflictos del motor + normativas de Error + cobertura + horas
                TotalConflicts = result.Conflicts.Count(c => c.Severity == ConflictSeverity.Error)
                               + normativeIssues.Count(c => c.Severity == ConflictSeverity.Error)
                               + coverageConflicts.Count
                               + teacherHoursConflicts.Count,
                CreatedBy = user.UserId,
            };
            db.Schedules.Add(schedule);

            foreach (var slot in result.AssignedSlots)
            {
                // ── Fix de aula: usar la elegida por el motor si es válida,
                //    caer al aula base del grupo / genérica solo como respaldo.
                Guid classroomId;
                if (slot.ClassroomId != Guid.Empty && validClassroomIds.Contains(slot.ClassroomId))
                    classroomId = slot.ClassroomId;
                else
                    classroomId = homeClassroomMap.GetValueOrDefault(slot.GroupId, defaultClassroomId);

                db.ScheduleEntries.Add(new DbScheduleEntry
                {
                    ScheduleId = schedule.Id, SchoolId = user.SchoolId,
                    GroupId = slot.GroupId, AllocationId = slot.AllocationId,
                    TeacherId = slot.TeacherId, ClassroomId = classroomId,
                    DayOfWeek = slot.DayOfWeek, SlotIndex = slot.SlotIndex,
                });
            }

            // Combinar conflictos del motor + incidencias normativas + cobertura + horas
            var allConflicts = result.Conflicts
                .Concat(normativeIssues)
                .Concat(coverageConflicts)
                .Concat(teacherHoursConflicts)
                .ToList();

            foreach (var conflict in allConflicts)
            {
                db.ScheduleConflicts.Add(new ScheduleConflictRecord
                {
                    ScheduleId = schedule.Id,
                    ConflictType = conflict.Type.ToString().ToLower(),
                    Severity = conflict.Severity.ToString().ToLower(),
                    Description = conflict.Description,
                    Suggestions = JsonSerializer.Serialize(conflict.Suggestions),
                    GroupId = conflict.GroupId, TeacherId = conflict.TeacherId,
                    DayOfWeek = conflict.DayOfWeek, SlotIndex = conflict.SlotIndex,
                });
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                scheduleId = schedule.Id,
                status = schedule.Status,
                totalAssigned = result.TotalAssigned,
                totalRequired = result.TotalRequired,
                elapsedSeconds = result.ElapsedSeconds,
                totalConflicts = schedule.TotalConflicts,
                totalCost = result.TotalCost,
            });
        });

        // GET /api/schedules/{id} — grid completo del horario
        g.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var schedule = await db.Schedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == user.SchoolId);
            if (schedule is null) return Results.NotFound();

            // Verificar que teacher puede ver solo published
            if (user.IsTeacher && schedule.Status != "published")
                return Results.StatusCode(403);

            return Results.Ok(await BuildGridDto(db, schedule));
        });

        // GET /api/schedules/me — horario del profesor autenticado
        g.MapGet("/me", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsTeacher) return Results.StatusCode(403);

            var teacher = await db.Teachers.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);
            if (teacher is null) return Results.NotFound(new { message = "Profesor no encontrado." });

            var schedule = await db.Schedules.AsNoTracking()
                .Where(s => s.SchoolId == user.SchoolId && s.Status == "published")
                .OrderByDescending(s => s.PublishedAt)
                .FirstOrDefaultAsync();

            if (schedule is null)
                return Results.NotFound(new { message = "El equipo directivo todavía no ha publicado el horario." });

            var school = await db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.SchoolId);

            var entries = await db.ScheduleEntries.AsNoTracking()
                .Where(e => e.ScheduleId == schedule.Id && e.TeacherId == teacher.Id)
                .ToListAsync();

            var allocations = await db.SubjectAllocations.AsNoTracking().ToDictionaryAsync(a => a.Id);
            var groups = await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId)
                .ToDictionaryAsync(g => g.Id);
            var classrooms = await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId)
                .ToDictionaryAsync(c => c.Id);

            var slots = school is not null ? SlotCalculator.Compute(school) : [];
            var lecSlots = slots.Where(s => !s.IsBreak).ToList();

            var myEntries = entries.Select(e =>
            {
                var alloc = allocations.GetValueOrDefault(e.AllocationId);
                var group = groups.GetValueOrDefault(e.GroupId);
                var classroom = classrooms.GetValueOrDefault(e.ClassroomId);
                var slotTime = lecSlots.Count > e.SlotIndex ? lecSlots[e.SlotIndex].StartTime : "?";
                return new MyScheduleEntry(
                    e.DayOfWeek, e.SlotIndex, slotTime,
                    alloc?.SubjectName ?? "?", alloc?.SubjectKey ?? "tut", alloc?.SubjectShort ?? "?",
                    group?.DisplayName ?? "?",
                    classroom?.Name ?? "?");
            }).ToList();

            return Results.Ok(new MyScheduleDto(
                teacher.FullName,
                school?.Name ?? "?",
                schedule.AcademicYear,
                myEntries,
                slots.Select(s => new SlotInfo(s.Index, s.StartTime, s.EndTime, s.IsBreak)).ToList()));
        });

        // POST /api/schedules/{id}/publish
        g.MapPost("/{id:guid}/publish", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var schedule = await db.Schedules
                .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == user.SchoolId);
            if (schedule is null) return Results.NotFound();
            if (schedule.Status == "published")
                return Results.BadRequest(new { message = "Este horario ya está publicado." });

            // Archivar el horario publicado anterior (inmutabilidad)
            var previous = await db.Schedules
                .Where(s => s.SchoolId == user.SchoolId && s.Status == "published")
                .ToListAsync();
            foreach (var prev in previous)
                prev.Status = "archived";

            schedule.Status = "published";
            schedule.PublishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Horario publicado. El anterior ha sido archivado." });
        });

        // PUT /api/schedules/{scheduleId}/entries/{entryId} — edición manual
        g.MapPut("/{scheduleId:guid}/entries/{entryId:guid}", async (
            Guid scheduleId, Guid entryId,
            HttpContext ctx, AppDbContext db, UpdateEntryRequest req) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            if (!user.IsAdmin) return Results.StatusCode(403);

            var schedule = await db.Schedules
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.SchoolId == user.SchoolId);
            if (schedule is null) return Results.NotFound();
            if (schedule.Status == "published")
                return Results.BadRequest(new { message = "No se puede editar un horario publicado. Crea una nueva versión." });

            var entry = await db.ScheduleEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.ScheduleId == scheduleId);
            if (entry is null) return Results.NotFound();

            entry.TeacherId = req.TeacherId;
            entry.ClassroomId = req.ClassroomId;
            entry.IsManualOverride = true;

            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Celda actualizada.", entryId });
        });

        return app;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<List<SessionToAssign>> BuildSessions(AppDbContext db, Guid schoolId, CancellationToken ct)
    {
        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId).ToListAsync(ct);
        var allocations = await db.SubjectAllocations.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, ct);
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .ToDictionaryAsync(t => t.Id, ct);
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schoolId)
            .ToDictionaryAsync(c => c.ClassroomType + "_" + c.Id, c => c.Id, ct);
        var groups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == schoolId)
            .ToDictionaryAsync(g => g.Id, ct);

        var sessions = new List<SessionToAssign>();
        foreach (var a in assignments)
        {
            if (!allocations.TryGetValue(a.AllocationId, out var alloc)) continue;
            if (!teachers.TryGetValue(a.TeacherId, out var teacher)) continue;
            
            var classroomType = alloc.RequiredClassroomType is not null
                ? ParseClassroomType(alloc.RequiredClassroomType)
                : (ClassroomType?)null;

            List<string> specialties;
            try
            {
                specialties = JsonSerializer.Deserialize<List<string>>(teacher.Specialties) ?? new List<string>();
            }
            catch
            {
                specialties = new List<string>();
            }
            int maxWeeklyHours = teacher.MaxWeeklyHours;
            var groupLabel = groups.TryGetValue(a.GroupId, out var grp) ? grp.DisplayName : "Grupo";

            // Generar una sesión por cada hora semanal
            for (int i = 0; i < a.WeeklyHours; i++)
            {
                sessions.Add(new SessionToAssign(
                    AssignmentId: a.Id,
                    GroupId: a.GroupId,
                    TeacherId: a.TeacherId,
                    AllocationId: a.AllocationId,
                    SubjectName: alloc.SubjectName,
                    GroupLabel: groupLabel,
                    RequiredClassroomId: null,
                    RequiredClassroomType: classroomType,
                    MaxConsecutiveSlots: alloc.MaxConsecutiveSlots,
                    RequiresSpecialist: alloc.RequiresSpecialist,
                    SubjectKey: alloc.SubjectKey ?? "",
                    TeacherSpecialties: specialties,
                    TeacherMaxWeeklyHours: maxWeeklyHours,
                    SplittableAcrossDays: alloc.SplittableAcrossDays));
            }
        }
        return sessions;
    }

    private static List<IHardConstraint> BuildHardConstraints(
        IReadOnlyList<(Guid TeacherId, int Day, int Slot)> unavailableSlots)
    {
        var result = new List<IHardConstraint>
        {
            new TeacherNotDoubleBooked(),
            new ClassroomNotDoubleBooked(),
            new MaxConsecutiveSlotsConstraint(),
            new RequiresSpecialistConstraint(),
            new MaxWeeklyHoursConstraint(),
        };
        if (unavailableSlots.Count > 0)
            result.Add(new TeacherAvailabilityConstraint(unavailableSlots));

        return result;
    }

    private static async Task<List<ISoftConstraint>> BuildSoftConstraints(
        AppDbContext db, Guid schoolId, int lastLectivoSlotIndex,
        SoftConstraintWeights weights, CancellationToken ct)
    {
        // Obtener asignaciones de Lengua/Matemáticas para el soft constraint de intensivas
        var intensiveIds = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId)
            .Join(db.SubjectAllocations,
                a => a.AllocationId,
                s => s.Id,
                (a, s) => new { a.AllocationId, s.SubjectKey })
            .Where(x => x.SubjectKey == "mat" || x.SubjectKey == "len")
            .Select(x => x.AllocationId)
            .Distinct()
            .ToListAsync(ct);

        return new List<ISoftConstraint>
        {
            new NoIntensiveSubjectLastSlot(lastLectivoSlotIndex, intensiveIds, weights.NoIntensiveSubjectLastSlot),
            new DistributeSubjectAcrossDays(weights.DistributeSubjectAcrossDays),
            new TeacherConsecutiveLoadConstraint(3, weights.TeacherConsecutiveLoad),
            new TeacherGapsConstraint(weights.TeacherGaps),
            new ConsecutiveBlockPreferenceConstraint(weights.ConsecutiveBlockPreference),
        };
    }

    private static ClassroomType ParseClassroomType(string t) => t.ToLower() switch
    {
        "gym"     => ClassroomType.Gym,
        "music"   => ClassroomType.Music,
        "lab"     => ClassroomType.Lab,
        "it"      => ClassroomType.IT,
        "support" => ClassroomType.Support,
        _         => ClassroomType.Regular,
    };

    private static async Task<ScheduleGridDto> BuildGridDto(AppDbContext db, ScheduleRecord schedule)
    {
        var entries = await db.ScheduleEntries.AsNoTracking()
            .Where(e => e.ScheduleId == schedule.Id).ToListAsync();
        var conflicts = await db.ScheduleConflicts.AsNoTracking()
            .Where(c => c.ScheduleId == schedule.Id).ToListAsync();
        var allocations = await db.SubjectAllocations.AsNoTracking().ToDictionaryAsync(a => a.Id);
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schedule.SchoolId).ToDictionaryAsync(t => t.Id);
        var groups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == schedule.SchoolId).ToDictionaryAsync(g => g.Id);
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schedule.SchoolId).ToDictionaryAsync(c => c.Id);
        var school = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schedule.SchoolId);
        var cycles = school is not null
            ? await db.CycleSchedules.AsNoTracking()
                .Where(c => c.SchoolId == schedule.SchoolId).ToListAsync()
            : [];

        var slots = school is not null ? SlotCalculator.Compute(school) : [];

        // Slots por ciclo (ciclo 1..3 → franja con horas reales del ciclo)
        var slotsByCycle = cycles
            .OrderBy(c => c.Cycle)
            .ToDictionary(
                c => c.Cycle,
                c => (IReadOnlyList<SlotInfo>)SlotCalculator
                    .Compute(school!, school!.SlotsPerDay, c.MorningStart, c.AfternoonStart)
                    .Select(s => new SlotInfo(s.Index, s.StartTime, s.EndTime, s.IsBreak))
                    .ToList());

        var gridEntries = entries.Select(e =>
        {
            var alloc = allocations.GetValueOrDefault(e.AllocationId);
            var teacher = teachers.GetValueOrDefault(e.TeacherId);
            var group = groups.GetValueOrDefault(e.GroupId);
            var classroom = classrooms.GetValueOrDefault(e.ClassroomId);
            return new ScheduleGridEntry(
                e.Id, e.DayOfWeek, e.SlotIndex,
                e.GroupId, group?.DisplayName ?? "?",
                e.TeacherId, teacher?.FullName ?? "?", teacher?.ColorKey ?? "mat",
                e.AllocationId, alloc?.SubjectName ?? "?", alloc?.SubjectKey ?? "tut", alloc?.SubjectShort ?? "?",
                e.ClassroomId, classroom?.Name ?? "?",
                e.IsManualOverride);
        }).ToList();

        var conflictDtos = conflicts.Select(c =>
        {
            string[] suggestions;
            try { suggestions = JsonSerializer.Deserialize<string[]>(c.Suggestions) ?? []; }
            catch { suggestions = []; }
            return new ConflictDto(c.ConflictType, c.Severity, c.Description, suggestions,
                c.GroupId, c.TeacherId, c.DayOfWeek, c.SlotIndex);
        }).ToList();

        return new ScheduleGridDto(
            schedule.Id, schedule.Status, schedule.AcademicYear,
            gridEntries, conflictDtos,
            slots.Select(s => new SlotInfo(s.Index, s.StartTime, s.EndTime, s.IsBreak)).ToList(),
            slotsByCycle);
    }

    private static string DayName(int day) => day switch
    {
        1 => "lunes", 2 => "martes", 3 => "miércoles",
        4 => "jueves", 5 => "viernes", 6 => "sábado", _ => "domingo"
    };
}
