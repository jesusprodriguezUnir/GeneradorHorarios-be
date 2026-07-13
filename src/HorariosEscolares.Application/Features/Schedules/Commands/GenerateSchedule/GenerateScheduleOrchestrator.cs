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
    ScheduleResultPersister persister,
    IScheduleEngine engine,
    INormativeValidator normativeValidator,
    ICycleResolver cycleResolver)
    : IScheduleGenerationOrchestrator
{
    public async Task<GenerateScheduleResult> GenerateAsync(
        Guid schoolId,
        Guid stageId,
        Guid periodId,
        string academicYear,
        Guid createdBy,
        int timeoutSeconds,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct)
    {
        var (stage, period) = await LoadCoreDataAsync(schoolId, stageId, periodId, ct);

        var periodHoursOverrides = await db.PeriodAssignmentHours.AsNoTracking()
            .Where(h => h.PeriodId == periodId)
            .ToDictionaryAsync(h => h.AssignmentId, h => h.WeeklyHours, ct);

        var sessions = await BuildSessionsAsync(schoolId, stage, periodHoursOverrides, ct);
        if (sessions.Count == 0)
            return new GenerateScheduleResult.NoAssignments();

        var (classrooms, unavailableSlots, unavailableSet) = await LoadAuxiliaryDataAsync(schoolId, ct);

        var workingDays = SlotCalculator.ParseWorkingDays(stage.WorkingDays);
        var cyclesList = BuildCycleGrids(period);
        var schoolConfig = BuildSchoolConfig(stage, period, cyclesList, classrooms);

        var normativeIssues = await ValidateNormativelyAsync(
            schoolConfig, stage, period, schoolId, stageId, ct);

        var context = await BuildGenerationContextAsync(
            schoolConfig, sessions, unavailableSlots, schoolId, period, timeoutSeconds, ct);

        var viabilityErrors = ScheduleViabilityAnalyzer.Analyze(context.School, sessions, unavailableSet);
        if (viabilityErrors.Any())
        {
            var failedScheduleId = await persister.PersistViabilityFailureAsync(
                schoolId, stageId, periodId, academicYear, createdBy, viabilityErrors, ct);
            return new GenerateScheduleResult.ViabilityFailed(
                failedScheduleId, viabilityErrors.Count, viabilityErrors);
        }

        var engineResult = await engine.GenerateAsync(context, ct, progress);

        var (coverageConflicts, teacherHoursConflicts) = await DetectPostProcessConflictsAsync(
            schoolId, stageId, stage, period, engineResult.AssignedSlots, ct);

        var scheduleId = await persister.PersistSuccessAsync(
            schoolId, stageId, periodId, academicYear, createdBy, engineResult,
            normativeIssues, coverageConflicts, teacherHoursConflicts,
            classrooms, ct);

        return new GenerateScheduleResult.Success(
            scheduleId, "generated",
            engineResult.TotalAssigned, engineResult.TotalRequired,
            engineResult.ElapsedSeconds,
            engineResult.Conflicts.Count(c => c.Severity == ConflictSeverity.Error)
                + normativeIssues.Count(c => c.Severity == ConflictSeverity.Error)
                + coverageConflicts.Count + teacherHoursConflicts.Count,
            engineResult.TotalCost);
    }

    private async Task<(SchoolStage Stage, SchoolPeriod Period)> LoadCoreDataAsync(
        Guid schoolId, Guid stageId, Guid periodId, CancellationToken ct)
    {
        var stage = await db.SchoolStages.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == stageId && s.SchoolId == schoolId, ct);
        if (stage is null)
            throw new NotFoundException("Etapa no encontrada.");

        var period = await db.SchoolPeriods.AsNoTracking()
            .Include(p => p.Cycles).ThenInclude(c => c.Breaks)
            .FirstOrDefaultAsync(p => p.Id == periodId && p.StageId == stageId, ct);
        if (period is null)
            throw new NotFoundException("Periodo no encontrado para esta etapa.");

        return (stage, period);
    }

    private async Task<(List<Classroom> Classrooms,
        List<(Guid TeacherId, int Day, int Slot)> UnavailableSlots,
        HashSet<(Guid TeacherId, int Day, int Slot)> UnavailableSet)> LoadAuxiliaryDataAsync(
        Guid schoolId, CancellationToken ct)
    {
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.SchoolId == schoolId).ToListAsync(ct);

        var unavailableSlots = await db.TeacherConstraints.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && c.ConstraintType == "unavailable")
            .Select(c => new { c.TeacherId, Day = c.DayOfWeek, Slot = c.SlotIndex })
            .ToListAsync(ct);
        var unavailableSet = unavailableSlots
            .Select(c => (c.TeacherId, c.Day, c.Slot)).ToHashSet();

        return (classrooms, unavailableSlots.Select(c => (c.TeacherId, c.Day, c.Slot)).ToList(), unavailableSet);
    }

    private SchoolConfig BuildSchoolConfig(
        SchoolStage stage, SchoolPeriod period, List<CycleGrid> cyclesList, List<Classroom> classrooms)
    {
        int maxLectiveSlots = cyclesList.Count > 0
            ? cyclesList.Max(cg => cg.Slots.Count(s => !s.IsBreak))
            : period.SlotsPerDay;

        var workingDays = SlotCalculator.ParseWorkingDays(stage.WorkingDays);

        return new SchoolConfig(
            maxLectiveSlots, workingDays.Count, workingDays, cyclesList,
            classrooms.Select(c => new ClassroomInfo(c.Id, c.Name, ParseClassroomType(c.ClassroomType))).ToList());
    }

    private async Task<List<ConflictExplanation>> ValidateNormativelyAsync(
        SchoolConfig schoolConfig,
        SchoolStage stage,
        SchoolPeriod period,
        Guid schoolId,
        Guid stageId,
        CancellationToken ct)
    {
        var stageGroupIds = (await db.CourseGroups.AsNoTracking()
            .Where(g => g.StageId == stageId)
            .Select(g => g.Id).ToListAsync(ct)).ToHashSet();

        var normativeAssignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId).ToListAsync(ct);
        var usedAllocationIds = normativeAssignments.Select(a => a.AllocationId).Distinct().ToList();
        var normativeAllocs = await db.SubjectAllocations.AsNoTracking()
            .Where(a => usedAllocationIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var normativeData = normativeAssignments
            .Where(a => stageGroupIds.Contains(a.GroupId) && normativeAllocs.ContainsKey(a.AllocationId))
            .Select(a => (a, normativeAllocs[a.AllocationId])).ToList();

        var normativeValidationData = new NormativeValidationData
        {
            SchoolConfig = schoolConfig,
            Stage = stage.StageType,
            MinCourseLevel = stage.MinLevel,
            MaxCourseLevel = stage.MaxLevel,
            BreakMinutes = stage.BreakMinutes,
            SlotMinutes = period.SlotMinutes,
            EnforceWeeklyLectiveMinimum = period.IsDefault,
            Assignments = normativeData.Select(n => new NormativeAssignmentData(
                n.a.GroupId, n.Item2.SubjectKey, n.Item2.SubjectName,
                n.a.WeeklyHours, n.Item2.WeeklyHoursMin,
                n.Item2.WeeklyHoursMax, n.Item2.WeeklyHoursDefault)).ToList(),
        };

        return await normativeValidator.ValidateAsync(normativeValidationData, ct);
    }

    private async Task<GenerationContext> BuildGenerationContextAsync(
        SchoolConfig schoolConfig,
        List<SessionToAssign> sessions,
        List<(Guid TeacherId, int Day, int Slot)> unavailableSlots,
        Guid schoolId,
        SchoolPeriod period,
        int timeoutSeconds,
        CancellationToken ct)
    {
        var referenceSlots = schoolConfig.Cycles.Count > 0
            ? schoolConfig.Cycles[0].Slots
            : Enumerable.Range(0, period.SlotsPerDay)
                .Select(i => new SlotConfig(i, false, i * period.SlotMinutes, (i + 1) * period.SlotMinutes)).ToList();

        int lastLectivoSlotIndex = referenceSlots.Where(s => !s.IsBreak).Any()
            ? referenceSlots.Where(s => !s.IsBreak).Max(s => s.Index)
            : (period.SlotsPerDay - 1);

        var weights = new SoftConstraintWeights();
        var hardConstraints = BuildHardConstraints(unavailableSlots);
        var softConstraints = await BuildSoftConstraintsAsync(schoolId, lastLectivoSlotIndex, weights, ct);

        return new GenerationContext
        {
            School = schoolConfig,
            Sessions = sessions,
            HardConstraints = hardConstraints,
            SoftConstraints = softConstraints,
            TimeoutSeconds = timeoutSeconds,
            Weights = weights,
        };
    }

    private async Task<(List<ConflictExplanation> Coverage, List<ConflictExplanation> TeacherHours)> DetectPostProcessConflictsAsync(
        Guid schoolId,
        Guid stageId,
        SchoolStage stage,
        SchoolPeriod period,
        IReadOnlyList<AssignedSlot> assignedSlots,
        CancellationToken ct)
    {
        var allGroups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.StageId == stageId)
            .ToDictionaryAsync(g => g.Id, ct);

        var stageGroupIds = allGroups.Keys.ToHashSet();
        var teacherAssignedWeeklyHoursExpected = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId && stageGroupIds.Contains(a.GroupId))
            .GroupBy(a => a.TeacherId)
            .Select(g => new { TeacherId = g.Key, ExpectedHours = g.Sum(a => a.WeeklyHours) })
            .ToDictionaryAsync(x => x.TeacherId, x => x.ExpectedHours, ct);
        var teacherNames = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .ToDictionaryAsync(t => t.Id, t => t.FullName, ct);

        var cyclesList = BuildCycleGrids(period);
        var referenceSlots = cyclesList.Count > 0
            ? cyclesList[0].Slots
            : Enumerable.Range(0, period.SlotsPerDay)
                .Select(i => new SlotConfig(i, false, i * period.SlotMinutes, (i + 1) * period.SlotMinutes)).ToList();

        var workingDays = SlotCalculator.ParseWorkingDays(stage.WorkingDays);
        var lectivoSlotIndices = referenceSlots.Where(s => !s.IsBreak).Select(s => s.Index).ToHashSet();
        var assignedSet = assignedSlots
            .Select(s => (s.GroupId, s.DayOfWeek, s.SlotIndex)).ToHashSet();

        var coverageConflicts = PostProcessConflictDetector.DetectCoverageConflicts(
            allGroups, workingDays, lectivoSlotIndices, assignedSet);

        var teacherHoursConflicts = PostProcessConflictDetector.DetectTeacherHoursConflicts(
            assignedSlots, teacherAssignedWeeklyHoursExpected, teacherNames);

        return (coverageConflicts, teacherHoursConflicts);
    }

    private List<CycleGrid> BuildCycleGrids(SchoolPeriod period)
    {
        var grids = new List<CycleGrid>();
        var presentCycles = period.Cycles.Select(c => c.Cycle).ToHashSet();

        foreach (var cs in period.Cycles)
        {
            var slots = SlotCalculator.Compute(cs, period);
            grids.Add(new CycleGrid(cs.Cycle,
                slots.Select(s => new SlotConfig(s.Index, s.IsBreak, s.StartMinute, s.EndMinute)).ToList()));
        }

        for (int c = 1; c <= 3; c++)
        {
            if (!presentCycles.Contains(c))
            {
                var isPartida = period.ScheduleType == "partida";
                var afternoonStart = isPartida ? new TimeOnly(15, 0) : (TimeOnly?)null;
                var fallbackSlots = SlotCalculator.Compute(period.SlotsPerDay, period.SlotMinutes, [], 0,
                    new TimeOnly(9, 0), afternoonStart, isPartida);
                grids.Add(new CycleGrid(c,
                    fallbackSlots.Select(s => new SlotConfig(s.Index, s.IsBreak, s.StartMinute, s.EndMinute)).ToList()));
            }
        }

        return grids;
    }

    private async Task<List<SessionToAssign>> BuildSessionsAsync(
        Guid schoolId,
        SchoolStage stage,
        IReadOnlyDictionary<Guid, int> periodHoursOverrides,
        CancellationToken ct)
    {
        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId).ToListAsync(ct);
        var assignmentAllocationIds = assignments.Select(a => a.AllocationId).Distinct().ToList();
        var allocations = await db.SubjectAllocations.AsNoTracking()
            .Where(a => assignmentAllocationIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .ToDictionaryAsync(t => t.Id, ct);
        var groups = await db.CourseGroups.AsNoTracking()
            .Where(g => g.StageId == stage.Id)
            .ToDictionaryAsync(g => g.Id, ct);

        var teacherSubjectHoursRaw = await db.TeacherSubjectHours.AsNoTracking()
            .Where(sh => teachers.Keys.Contains(sh.TeacherId))
            .ToListAsync(ct);
        var teacherSubjectHoursMap = teacherSubjectHoursRaw
            .GroupBy(sh => sh.TeacherId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, int>)g.ToDictionary(sh => sh.SubjectKey, sh => sh.WeeklyHours));

        var sessions = new List<SessionToAssign>();
        foreach (var a in assignments)
        {
            if (!groups.TryGetValue(a.GroupId, out var grp)) continue;
            if (!allocations.TryGetValue(a.AllocationId, out var alloc)) continue;
            if (!teachers.TryGetValue(a.TeacherId, out var teacher)) continue;

            var classroomType = alloc.RequiredClassroomType is not null
                ? ParseClassroomType(alloc.RequiredClassroomType) : (ClassroomType?)null;

            var subjectHours = teacherSubjectHoursMap.TryGetValue(teacher.Id, out var sh)
                ? sh : (IReadOnlyDictionary<string, int>)new Dictionary<string, int>();

            var groupLabel = grp.DisplayName;
            var cycle = cycleResolver.ResolveCycle(stage.StageType, grp.CourseLevel);

            var hoursToUse = periodHoursOverrides.GetValueOrDefault(a.Id, a.WeeklyHours);

            for (int i = 0; i < hoursToUse; i++)
            {
                sessions.Add(new SessionToAssign(
                    AssignmentId: a.Id, GroupId: a.GroupId, TeacherId: a.TeacherId,
                    AllocationId: a.AllocationId, SubjectName: alloc.SubjectName,
                    GroupLabel: groupLabel, RequiredClassroomId: null,
                    RequiredClassroomType: classroomType,
                    MaxConsecutiveSlots: alloc.MaxConsecutiveSlots,
                    RequiresSpecialist: alloc.RequiresSpecialist,
                    SubjectKey: alloc.SubjectKey ?? "",
                    TeacherSubjectHours: subjectHours,
                    TeacherMaxWeeklyHours: teacher.MaxWeeklyHours,
                    SplittableAcrossDays: alloc.SplittableAcrossDays,
                    Cycle: cycle));
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
            new TeacherSubjectHoursConstraint(), new MaxWeeklyHoursConstraint(),
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

    private static ClassroomType ParseClassroomType(string t) => t.ToLowerInvariant() switch
    {
        "gym" => ClassroomType.Gym, "music" => ClassroomType.Music,
        "lab" => ClassroomType.Lab, "it" => ClassroomType.IT,
        "support" => ClassroomType.Support, _ => ClassroomType.Regular,
    };

}
