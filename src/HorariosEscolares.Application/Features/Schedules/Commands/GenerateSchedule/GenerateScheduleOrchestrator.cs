using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Scheduling;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

public sealed class GenerateScheduleOrchestrator(
    IAppDbContext db,
    IScheduleRepository scheduleRepository,
    IScheduleEngine engine,
    INormativeValidator normativeValidator)
    : IScheduleGenerationOrchestrator
{
    public async Task<GenerateScheduleResult> GenerateAsync(
        Guid schoolId,
        string academicYear,
        int timeoutSeconds,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct)
    {
        // ── 1. Cargar colegio ─────────────────────────────────────────────────
        var school = await db.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school is null)
            throw new NotFoundException("Colegio no encontrado.");

        // ── 2. Construir sesiones ─────────────────────────────────────────────
        var sessions = await BuildSessionsAsync(schoolId, ct);
        if (sessions.Count == 0)
            return new GenerateScheduleResult.NoAssignments();

        // ── 3. Cargar datos auxiliares ────────────────────────────────────────
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schoolId).ToListAsync(ct);

        var unavailableSlots = await db.TeacherConstraints.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && c.ConstraintType == "unavailable")
            .Select(c => new { c.TeacherId, Day = c.DayOfWeek, Slot = c.SlotIndex })
            .ToListAsync(ct);
        var unavailableSet = unavailableSlots
            .Select(c => (c.TeacherId, c.Day, c.Slot)).ToHashSet();

        // ── 4. Validación normativa ───────────────────────────────────────────
        var normativeAllocs = await db.SubjectAllocations.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, ct);
        var normativeAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId).ToListAsync(ct);
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

        // ── 5. Computar slots y constraints ──────────────────────────────────
        var slots = SlotCalculator.Compute(school, school.SlotsPerDay);
        int lastLectivoSlotIndex = slots.Where(s => !s.IsBreak).Any()
            ? slots.Where(s => !s.IsBreak).Max(s => s.Index) : 4;

        var weights = new SoftConstraintWeights();
        var hardConstraints = BuildHardConstraints(unavailableSlots
            .Select(c => (c.TeacherId, c.Day, c.Slot)).ToList());
        var softConstraints = await BuildSoftConstraintsAsync(schoolId, lastLectivoSlotIndex, weights, ct);

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
            TimeoutSeconds = timeoutSeconds,
            Weights = weights,
        };

        // ── 6. Análisis de viabilidad ─────────────────────────────────────────
        var viabilityErrors = ScheduleViabilityAnalyzer.Analyze(context.School, sessions, unavailableSet);
        if (viabilityErrors.Any())
        {
            var failedSchedule = new ScheduleRecord
            {
                SchoolId = schoolId, AcademicYear = academicYear,
                Status = "failed",
                GeneratedAt = DateTime.UtcNow, GenerationSeconds = 0,
                TotalConflicts = viabilityErrors.Count,
                CreatedBy = Guid.Empty, // El endpoint sobrescribe esto si es necesario
            };

            var failedConflicts = viabilityErrors.Select(conflict => new ScheduleConflictRecord
            {
                ScheduleId = failedSchedule.Id,
                ConflictType = conflict.Type.ToString().ToLower(),
                Severity = conflict.Severity.ToString().ToLower(),
                Description = conflict.Description,
                Suggestions = JsonSerializer.Serialize(conflict.Suggestions),
                GroupId = conflict.GroupId, TeacherId = conflict.TeacherId,
                DayOfWeek = conflict.DayOfWeek, SlotIndex = conflict.SlotIndex,
            }).ToList();

            await scheduleRepository.AddScheduleWithDetailsAsync(failedSchedule, [], failedConflicts, ct);

            return new GenerateScheduleResult.ViabilityFailed(
                failedSchedule.Id, viabilityErrors.Count, viabilityErrors);
        }

        // ── 7. Ejecutar motor de generación ──────────────────────────────────
        var result = await engine.GenerateAsync(context, ct, progress);

        // ── 8. Post-procesamiento ────────────────────────────────────────────
        var homeClassroomMap = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == schoolId && g.HomeClassroomId.HasValue)
            .ToDictionaryAsync(g => g.Id, g => g.HomeClassroomId!.Value, ct);
        var defaultClassroomId = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && c.ClassroomType == "regular")
            .Select(c => c.Id).FirstOrDefaultAsync(ct);
        var validClassroomIds = (await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schoolId)
            .Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var allGroups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.SchoolId == schoolId)
            .ToDictionaryAsync(g => g.Id, ct);
        var teacherAssignedWeeklyHoursExpected = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId)
            .GroupBy(a => a.TeacherId)
            .Select(g => new { TeacherId = g.Key, ExpectedHours = g.Sum(a => a.WeeklyHours) })
            .ToDictionaryAsync(x => x.TeacherId, x => x.ExpectedHours, ct);
        var teacherNames = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);

        var workingDaysList = SlotCalculator.ParseWorkingDays(school.WorkingDays);
        var lectivoSlotIndices = slots.Where(s => !s.IsBreak).Select(s => s.Index).ToHashSet();
        var assignedSet = result.AssignedSlots
            .Select(s => (s.GroupId, s.DayOfWeek, s.SlotIndex)).ToHashSet();

        var coverageConflicts = DetectCoverageConflicts(
            allGroups, workingDaysList, lectivoSlotIndices, assignedSet);

        var teacherHoursConflicts = DetectTeacherHoursConflicts(
            result.AssignedSlots, teacherAssignedWeeklyHoursExpected, teacherNames);

        // ── 9. Persistir resultado ────────────────────────────────────────────
        var schedule = new ScheduleRecord
        {
            SchoolId = schoolId, AcademicYear = academicYear,
            Status = "generated",
            GeneratedAt = DateTime.UtcNow, GenerationSeconds = result.ElapsedSeconds,
            TotalConflicts = result.Conflicts.Count(c => c.Severity == ConflictSeverity.Error)
                           + normativeIssues.Count(c => c.Severity == ConflictSeverity.Error)
                           + coverageConflicts.Count + teacherHoursConflicts.Count,
            CreatedBy = Guid.Empty,
        };

        var entries = result.AssignedSlots.Select(slot =>
        {
            Guid classroomId;
            if (slot.ClassroomId != Guid.Empty && validClassroomIds.Contains(slot.ClassroomId))
                classroomId = slot.ClassroomId;
            else
                classroomId = homeClassroomMap.GetValueOrDefault(slot.GroupId, defaultClassroomId);

            return new ScheduleEntry
            {
                ScheduleId = schedule.Id, SchoolId = schoolId,
                GroupId = slot.GroupId, AllocationId = slot.AllocationId,
                TeacherId = slot.TeacherId, ClassroomId = classroomId,
                DayOfWeek = slot.DayOfWeek, SlotIndex = slot.SlotIndex,
            };
        }).ToList();

        var allConflicts = result.Conflicts
            .Concat(normativeIssues).Concat(coverageConflicts).Concat(teacherHoursConflicts).ToList();

        var conflictRecords = allConflicts.Select(conflict => new ScheduleConflictRecord
        {
            ScheduleId = schedule.Id,
            ConflictType = conflict.Type.ToString().ToLower(),
            Severity = conflict.Severity.ToString().ToLower(),
            Description = conflict.Description,
            Suggestions = JsonSerializer.Serialize(conflict.Suggestions),
            GroupId = conflict.GroupId, TeacherId = conflict.TeacherId,
            DayOfWeek = conflict.DayOfWeek, SlotIndex = conflict.SlotIndex,
        }).ToList();

        await scheduleRepository.AddScheduleWithDetailsAsync(schedule, entries, conflictRecords, ct);

        return new GenerateScheduleResult.Success(
            schedule.Id, schedule.Status,
            result.TotalAssigned, result.TotalRequired,
            result.ElapsedSeconds, schedule.TotalConflicts, result.TotalCost);
    }

    // ── Métodos privados ──────────────────────────────────────────────────────

    private async Task<List<SessionToAssign>> BuildSessionsAsync(Guid schoolId, CancellationToken ct)
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

    private async Task<List<ISoftConstraint>> BuildSoftConstraintsAsync(
        Guid schoolId, int lastLectivoSlotIndex,
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

    private static List<ConflictExplanation> DetectCoverageConflicts(
        IReadOnlyDictionary<Guid, CourseGroup> allGroups,
        IReadOnlyList<int> workingDays,
        IReadOnlySet<int> lectivoSlotIndices,
        IReadOnlySet<(Guid GroupId, int DayOfWeek, int SlotIndex)> assignedSet)
    {
        var conflicts = new List<ConflictExplanation>();
        foreach (var (groupId, group) in allGroups)
        {
            foreach (var day in workingDays)
            {
                foreach (var slotIdx in lectivoSlotIndices)
                {
                    if (!assignedSet.Contains((groupId, day, slotIdx)))
                    {
                        conflicts.Add(new ConflictExplanation
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
        return conflicts;
    }

    private static List<ConflictExplanation> DetectTeacherHoursConflicts(
        IReadOnlyList<AssignedSlot> assignedSlots,
        IReadOnlyDictionary<Guid, int> teacherExpectedHours,
        IReadOnlyDictionary<Guid, string> teacherNames)
    {
        var teacherActualHours = assignedSlots
            .GroupBy(s => s.TeacherId)
            .ToDictionary(g => g.Key, g => g.Count());

        var conflicts = new List<ConflictExplanation>();
        foreach (var (teacherId, expectedHours) in teacherExpectedHours)
        {
            var actual = teacherActualHours.GetValueOrDefault(teacherId, 0);
            if (actual < expectedHours)
            {
                var name = teacherNames.GetValueOrDefault(teacherId, "Profesor");
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Teacher, Severity = ConflictSeverity.Warning,
                    Description = $"{name} tiene {actual} horas asignadas de {expectedHours} configuradas.",
                    Suggestions = ["Comprueba que no haya conflictos de disponibilidad o aulas especiales sin cubrir."],
                    TeacherId = teacherId,
                });
            }
        }
        return conflicts;
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
