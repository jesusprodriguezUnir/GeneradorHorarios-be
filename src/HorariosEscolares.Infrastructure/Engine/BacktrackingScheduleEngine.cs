using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Engine;

public sealed class BacktrackingScheduleEngine : IScheduleEngine
{
    private sealed class BestSolutionTracker
    {
        private readonly object _lock = new();
        public List<AssignedSlot> Best { get; set; } = [];

        public void Update(IReadOnlyList<AssignedSlot> current)
        {
            lock (_lock)
            {
                if (current.Count > Best.Count)
                    Best = [.. current];
            }
        }
    }

    public async Task<ScheduleResult> GenerateAsync(
        GenerationContext context,
        CancellationToken cancellationToken,
        IProgress<GenerationProgress>? progress = null)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(context.TimeoutSeconds));

        var startTime = DateTime.UtcNow;
        var sessions = PrioritizeSessions(context.Sessions);
        var state = new AssignmentState(context.School);
        var tracker = new BestSolutionTracker();

        var result = await Task.Run(
            () => Backtrack(sessions, 0, state, context, progress, tracker, timeoutCts.Token),
            cancellationToken);

        var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
        var conflicts = BuildConflictExplanations(sessions, result, context);
        var totalCost = ComputeScheduleCost(result, sessions, context.SoftConstraints);

        return new ScheduleResult
        {
            Status = conflicts.Any(c => c.Severity == ConflictSeverity.Error)
                ? GenerationStatus.Partial
                : GenerationStatus.Complete,
            AssignedSlots = result,
            Conflicts = conflicts.OrderBy(c => c.Type).ThenBy(c => c.Severity).ToList(),
            ElapsedSeconds = elapsed,
            TotalRequired = sessions.Count,
            TotalCost = totalCost,
        };
    }

    private static List<SessionToAssign> PrioritizeSessions(
        IReadOnlyList<SessionToAssign> sessions)
    {
        var teacherTotalHours = sessions
            .GroupBy(s => s.TeacherId)
            .ToDictionary(g => g.Key, g => g.Count());

        return [.. sessions.OrderByDescending(s =>
        {
            int score = 0;
            if (s.RequiredClassroomType is not null) score += 100;
            if (s.MaxConsecutiveSlots == 1) score += 50;
            if (!s.SplittableAcrossDays) score += 40;

            int tHours = teacherTotalHours.GetValueOrDefault(s.TeacherId, 0);
            score += tHours * 2;

            return score;
        })];
    }

    private static List<AssignedSlot> Backtrack(
        List<SessionToAssign> sessions,
        int index,
        AssignmentState state,
        GenerationContext context,
        IProgress<GenerationProgress>? progress,
        BestSolutionTracker tracker,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return tracker.Best;

        if (index >= sessions.Count)
            return [.. state.Assigned];

        var session = sessions[index];
        var candidates = GetCandidateSlots(session, state, context);

        foreach (var (day, slot, startMin, endMin, classroom) in candidates)
        {
            if (ct.IsCancellationRequested) break;

            state.Assign(session, day, slot, startMin, endMin, classroom);
            tracker.Update(state.Assigned);

            progress?.Report(new GenerationProgress(
                state.Assigned.Count,
                sessions.Count,
                $"Asignando {session.SubjectName} a {session.GroupLabel}..."));

            var result = Backtrack(sessions, index + 1, state, context, progress, tracker, ct);

            if (!ct.IsCancellationRequested && result.Count == sessions.Count)
                return result;

            state.Unassign(session, day, slot, startMin, endMin, classroom);
        }

        return tracker.Best;
    }

    private static List<(int Day, int Slot, int StartMinute, int EndMinute, Guid ClassroomId)> GetCandidateSlots(
        SessionToAssign session,
        AssignmentState state,
        GenerationContext context)
    {
        var candidates = new List<(int Day, int Slot, int StartMinute, int EndMinute, Guid ClassroomId, int Penalty)>();

        var cycleSlots = context.School.SlotsFor(session.Cycle);

        foreach (var day in context.School.WorkingDays)
        {
            foreach (var slotConfig in cycleSlots)
            {
                if (slotConfig.IsBreak) continue;
                int slot = slotConfig.Index;

                if (state.IsTeacherBusy(session.TeacherId, day, slotConfig.StartMinute, slotConfig.EndMinute)) continue;

                var availableClassroom = state.FindAvailableClassroom(
                    day, slotConfig.StartMinute, slotConfig.EndMinute, session.RequiredClassroomType, context.School.Classrooms);

                if (availableClassroom is null) continue;

                var testEntry = new ProposedEntry(session, day, slot, availableClassroom.Value, slotConfig.StartMinute, slotConfig.EndMinute);
                bool hardViolation = context.HardConstraints
                    .Any(c => !c.IsSatisfied(testEntry, state));

                if (hardViolation) continue;

                int penalty = context.SoftConstraints
                    .Sum(c => c.Penalty(testEntry, state));

                candidates.Add((day, slot, slotConfig.StartMinute, slotConfig.EndMinute, availableClassroom.Value, penalty));
            }
        }

        return [.. candidates
            .OrderBy(c => c.Penalty)
            .Select(c => (c.Day, c.Slot, c.StartMinute, c.EndMinute, c.ClassroomId))];
    }

    public static int ComputeScheduleCost(
        IReadOnlyList<AssignedSlot> schedule,
        IReadOnlyList<SessionToAssign> sessions,
        IReadOnlyList<ISoftConstraint> softConstraints)
    {
        if (schedule.Count == 0 || softConstraints.Count == 0) return 0;

        var sessionIndex = sessions
            .GroupBy(s => s.AssignmentId)
            .ToDictionary(g => g.Key, g => g.First());

        var state = new AssignmentState(
            new SchoolConfig(0, 0, [], [], []));

        int totalCost = 0;

        foreach (var slot in schedule)
        {
            if (!sessionIndex.TryGetValue(slot.AssignmentId, out var session))
                continue;

            var entry = new ProposedEntry(session, slot.DayOfWeek, slot.SlotIndex, slot.ClassroomId, slot.StartMinute, slot.EndMinute);

            totalCost += softConstraints.Sum(c => c.Penalty(entry, state));

            state.Assign(session, slot.DayOfWeek, slot.SlotIndex, slot.StartMinute, slot.EndMinute, slot.ClassroomId);
        }

        return totalCost;
    }

    private static List<ConflictExplanation> BuildConflictExplanations(
        List<SessionToAssign> sessions,
        List<AssignedSlot> assigned,
        GenerationContext context)
    {
        var assignedIds = assigned.Select(a => a.AssignmentId).ToHashSet();
        var conflicts = new List<ConflictExplanation>();

        foreach (var session in sessions.Where(s => !assignedIds.Contains(s.AssignmentId)))
        {
            conflicts.Add(ConflictExplanationBuilder.Build(session, assigned, context));
        }

        return conflicts;
    }
}
