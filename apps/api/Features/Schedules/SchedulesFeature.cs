using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MediatR;
using HorariosEscolares.Application.Features.Schedules;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Features.Schools;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Infrastructure.Persistence;
using DbScheduleEntry = HorariosEscolares.Domain.Entities.ScheduleEntry;
using DbScheduleConflict = HorariosEscolares.Domain.Entities.ScheduleConflictRecord;

namespace HorariosEscolares.Features.Schedules;

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

public record GenerateRequest(string AcademicYear, int TimeoutSeconds = 30);
public record UpdateEntryRequest(Guid TeacherId, Guid ClassroomId);

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/schedules");

        // GET /api/schedules — lista de horarios del colegio
        g.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetSchedulesListQuery());
            return Results.Ok(result);
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

            var sessions = await BuildSessions(db, user.SchoolId, ct);
            if (sessions.Count == 0)
                return Results.BadRequest(new { message = "No hay asignaciones configuradas." });

            var classrooms = await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId).ToListAsync(ct);

            var unavailableSlots = await db.TeacherConstraints.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId && c.ConstraintType == "unavailable")
                .Select(c => new { c.TeacherId, Day = c.DayOfWeek, Slot = c.SlotIndex })
                .ToListAsync(ct);
            var unavailableSet = unavailableSlots
                .Select(c => (c.TeacherId, c.Day, c.Slot)).ToHashSet();

            var normativeAllocs = await db.SubjectAllocations.AsNoTracking()
                .ToDictionaryAsync(a => a.Id, ct);
            var normativeAssignments = await db.Assignments.AsNoTracking()
                .Where(a => a.SchoolId == user.SchoolId).ToListAsync(ct);
            var normativeData = normativeAssignments
                .Where(a => normativeAllocs.ContainsKey(a.AllocationId))
                .Select(a => (a, normativeAllocs[a.AllocationId])).ToList();
            var normativeValidationData = new NormativeValidationData(
                new SchoolConfig(school.SlotsPerDay, school.DaysPerWeek,
                    SlotCalculator.ParseWorkingDays(school.WorkingDays).ToList(),
                    SlotCalculator.Compute(school, school.SlotsPerDay)
                        .Select(s => new SlotConfig(s.Index, s.IsBreak)).ToList(), []),
                school.Stage, school.MinCourseLevel, school.MaxCourseLevel,
                school.BreakMinutes, school.SlotMinutes,
                normativeData.Select(n => new NormativeAssignmentData(
                    n.a.GroupId, n.Item2.SubjectKey, n.Item2.SubjectName,
                    n.a.WeeklyHours, n.Item2.WeeklyHoursMin,
                    n.Item2.WeeklyHoursMax, n.Item2.WeeklyHoursDefault)).ToList());
            var normativeIssues = await normativeValidator.ValidateAsync(normativeValidationData, ct);

            var slots = SlotCalculator.Compute(school, school.SlotsPerDay);
            int lastLectivoSlotIndex = slots.Where(s => !s.IsBreak).Any()
                ? slots.Where(s => !s.IsBreak).Max(s => s.Index) : 4;

            var weights = new SoftConstraintWeights();
            var hardConstraints = BuildHardConstraints(unavailableSlots
                .Select(c => (c.TeacherId, c.Day, c.Slot)).ToList());
            var softConstraints = await BuildSoftConstraints(db, user.SchoolId, lastLectivoSlotIndex, weights, ct);

            var workingDays = SlotCalculator.ParseWorkingDays(school.WorkingDays);
            var context = new GenerationContext
            {
                School = new SchoolConfig(
                    school.SlotsPerDay, school.DaysPerWeek, workingDays,
                    slots.Select(s => new SlotConfig(s.Index, s.IsBreak)).ToList(),
                    classrooms.Select(c => new ClassroomInfo(c.Id, c.Name, ParseClassroomType(c.ClassroomType))).ToList()
                ),
                Sessions = sessions,
                HardConstraints = hardConstraints,
                SoftConstraints = softConstraints,
                TimeoutSeconds = req.TimeoutSeconds,
                Weights = weights,
            };

            var viabilityErrors = ScheduleViabilityAnalyzer.Analyze(context.School, sessions, unavailableSet);
            if (viabilityErrors.Any())
            {
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
                return Results.BadRequest(new
                {
                    scheduleId = failedSchedule.Id, status = "failed",
                    totalAssigned = 0, totalRequired = sessions.Count,
                    elapsedSeconds = 0, totalConflicts = viabilityErrors.Count,
                    conflicts = viabilityErrors.Select(c => new
                    {
                        type = c.Type.ToString().ToLower(),
                        severity = c.Severity.ToString().ToLower(),
                        description = c.Description,
                        suggestions = c.Suggestions,
                        teacherId = c.TeacherId, groupId = c.GroupId,
                    }),
                });
            }

            var progress = new Progress<GenerationProgress>(async p =>
            {
                try
                {
                    await hub.Clients.Group(user.SchoolId.ToString())
                        .SendAsync("Progress", new
                        {
                            assigned = p.Assigned, total = p.Total,
                            percentage = p.Percentage, currentAction = p.CurrentAction,
                        }, ct);
                }
                catch { }
            });

            var result = await engine.GenerateAsync(context, ct, progress);

            var homeClassroomMap = await db.CourseGroups.AsNoTracking()
                .Where(g => g.SchoolId == user.SchoolId && g.HomeClassroomId.HasValue)
                .ToDictionaryAsync(g => g.Id, g => g.HomeClassroomId!.Value, ct);
            var defaultClassroomId = await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId && c.ClassroomType == "regular")
                .Select(c => c.Id).FirstOrDefaultAsync(ct);
            var validClassroomIds = (await db.Classrooms.AsNoTracking()
                .Where(c => c.SchoolId == user.SchoolId)
                .Select(c => c.Id).ToListAsync(ct)).ToHashSet();
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

            var workingDaysList = SlotCalculator.ParseWorkingDays(school.WorkingDays);
            var lectivoSlotIndices = slots.Where(s => !s.IsBreak).Select(s => s.Index).ToHashSet();
            var assignedSet = result.AssignedSlots
                .Select(s => (s.GroupId, s.DayOfWeek, s.SlotIndex)).ToHashSet();

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
                                Type = ConflictType.Coverage, Severity = ConflictSeverity.Warning,
                                Description = $"Hueco sin cubrir para {group.DisplayName} el {DayName(day)} en la hora {slotIdx + 1}.",
                                Suggestions = ["Revisa las asignaciones del grupo o amplía sus horas lectivas."],
                                GroupId = groupId, DayOfWeek = day, SlotIndex = slotIdx,
                            });
                        }
                    }
                }
            }

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
                        Type = ConflictType.Teacher, Severity = ConflictSeverity.Warning,
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
                TotalConflicts = result.Conflicts.Count(c => c.Severity == ConflictSeverity.Error)
                               + normativeIssues.Count(c => c.Severity == ConflictSeverity.Error)
                               + coverageConflicts.Count + teacherHoursConflicts.Count,
                CreatedBy = user.UserId,
            };
            db.Schedules.Add(schedule);

            foreach (var slot in result.AssignedSlots)
            {
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

            var allConflicts = result.Conflicts
                .Concat(normativeIssues).Concat(coverageConflicts).Concat(teacherHoursConflicts).ToList();

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
                scheduleId = schedule.Id, status = schedule.Status,
                totalAssigned = result.TotalAssigned, totalRequired = result.TotalRequired,
                elapsedSeconds = result.ElapsedSeconds, totalConflicts = schedule.TotalConflicts,
                totalCost = result.TotalCost,
            });
        });

        // GET /api/schedules/{id} — grid completo del horario
        g.MapGet("/{id:guid}", async (Guid id, ISender sender, HttpContext ctx) =>
        {
            var user = ctx.GetCurrentUserOrFail();
            var result = await sender.Send(new GetScheduleGridQuery(id));
            if (result is null) return Results.NotFound();
            if (user.IsTeacher && result.Status != "published") return Results.StatusCode(403);
            return Results.Ok(result);
        });

        // GET /api/schedules/me — horario del profesor autenticado
        g.MapGet("/me", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMyScheduleQuery());
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // POST /api/schedules/{id}/publish
        g.MapPost("/{id:guid}/publish", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                var message = await sender.Send(new PublishScheduleCommand(id));
                return Results.Ok(new { message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // PUT /api/schedules/{scheduleId}/entries/{entryId}
        g.MapPut("/{scheduleId:guid}/entries/{entryId:guid}", async (
            Guid scheduleId, Guid entryId,
            HttpContext ctx, ISender sender, UpdateEntryRequest req) =>
        {
            if (!ctx.GetCurrentUserOrFail().IsAdmin) return Results.StatusCode(403);
            try
            {
                await sender.Send(new UpdateScheduleEntryCommand(scheduleId, entryId,
                    new Application.Features.Schedules.UpdateEntryRequest(req.TeacherId, req.ClassroomId)));
                return Results.Ok(new { message = "Celda actualizada.", entryId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }

    private static async Task<List<SessionToAssign>> BuildSessions(AppDbContext db, Guid schoolId, CancellationToken ct)
    {
        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId).ToListAsync(ct);
        var allocations = await db.SubjectAllocations.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, ct);
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .ToDictionaryAsync(t => t.Id, ct);
        var groups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == schoolId)
            .ToDictionaryAsync(g => g.Id, ct);

        var sessions = new List<SessionToAssign>();
        foreach (var a in assignments)
        {
            if (!allocations.TryGetValue(a.AllocationId, out var alloc)) continue;
            if (!teachers.TryGetValue(a.TeacherId, out var teacher)) continue;

            var classroomType = alloc.RequiredClassroomType is not null
                ? ParseClassroomType(alloc.RequiredClassroomType) : (ClassroomType?)null;

            List<string> specialties;
            try { specialties = JsonSerializer.Deserialize<List<string>>(teacher.Specialties) ?? []; }
            catch { specialties = []; }
            var groupLabel = groups.TryGetValue(a.GroupId, out var grp) ? grp.DisplayName : "Grupo";

            for (int i = 0; i < a.WeeklyHours; i++)
            {
                sessions.Add(new SessionToAssign(
                    AssignmentId: a.Id, GroupId: a.GroupId, TeacherId: a.TeacherId,
                    AllocationId: a.AllocationId, SubjectName: alloc.SubjectName,
                    GroupLabel: groupLabel, RequiredClassroomId: null,
                    RequiredClassroomType: classroomType,
                    MaxConsecutiveSlots: alloc.MaxConsecutiveSlots,
                    RequiresSpecialist: alloc.RequiresSpecialist,
                    SubjectKey: alloc.SubjectKey ?? "",
                    TeacherSpecialties: specialties,
                    TeacherMaxWeeklyHours: teacher.MaxWeeklyHours,
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
            new TeacherNotDoubleBooked(), new ClassroomNotDoubleBooked(),
            new MaxConsecutiveSlotsConstraint(), new RequiresSpecialistConstraint(),
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
        var intensiveIds = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId)
            .Join(db.SubjectAllocations, a => a.AllocationId, s => s.Id,
                (a, s) => new { a.AllocationId, s.SubjectKey })
            .Where(x => x.SubjectKey == "mat" || x.SubjectKey == "len")
            .Select(x => x.AllocationId).Distinct().ToListAsync(ct);

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
        "gym" => ClassroomType.Gym, "music" => ClassroomType.Music,
        "lab" => ClassroomType.Lab, "it" => ClassroomType.IT,
        "support" => ClassroomType.Support, _ => ClassroomType.Regular,
    };

    private static string DayName(int day) => day switch
    {
        1 => "lunes", 2 => "martes", 3 => "miércoles",
        4 => "jueves", 5 => "viernes", 6 => "sábado", _ => "domingo"
    };
}
