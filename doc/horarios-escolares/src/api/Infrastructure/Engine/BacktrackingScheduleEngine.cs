using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Engine;

/// <summary>
/// Motor de generación de horarios por backtracking con propagación de constraints.
/// Las sesiones más difíciles de asignar se intentan primero (fail-first heuristic).
/// </summary>
public sealed class BacktrackingScheduleEngine : IScheduleEngine
{
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

        var result = await Task.Run(
            () => Backtrack(sessions, 0, state, context, timeoutCts.Token, progress),
            cancellationToken);

        var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
        var conflicts = BuildConflictExplanations(sessions, result, context);

        return new ScheduleResult
        {
            Status = conflicts.Any(c => c.Severity == ConflictSeverity.Error)
                ? GenerationStatus.Partial
                : GenerationStatus.Complete,
            AssignedSlots = result,
            Conflicts = conflicts.OrderBy(c => c.Type).ThenBy(c => c.Severity).ToList(),
            ElapsedSeconds = elapsed,
            TotalRequired = sessions.Count
        };
    }

    // ── Heurística: sesiones más restringidas primero ─────────────────────────

    private static List<SessionToAssign> PrioritizeSessions(
        IReadOnlyList<SessionToAssign> sessions)
    {
        return [.. sessions.OrderByDescending(s =>
        {
            int score = 0;
            if (s.RequiredClassroomType is not null) score += 100; // aula especial → más difícil
            if (s.MaxConsecutiveSlots == 1) score += 50;           // no puede ir consecutiva
            return score;
        })];
    }

    // ── Backtracking recursivo ────────────────────────────────────────────────

    private List<AssignedSlot> Backtrack(
        List<SessionToAssign> sessions,
        int index,
        AssignmentState state,
        GenerationContext context,
        CancellationToken ct,
        IProgress<GenerationProgress>? progress)
    {
        if (ct.IsCancellationRequested || index >= sessions.Count)
            return [.. state.Assigned];

        var session = sessions[index];
        var candidates = GetCandidateSlots(session, state, context);

        foreach (var (day, slot, classroom) in candidates)
        {
            if (ct.IsCancellationRequested) break;

            state.Assign(session, day, slot, classroom);

            progress?.Report(new GenerationProgress(
                state.Assigned.Count,
                sessions.Count,
                $"Asignando {session.SubjectName} a {session.GroupLabel}..."));

            var result = Backtrack(sessions, index + 1, state, context, ct, progress);

            // Si llegamos al final sin cancelar, éxito total
            if (!ct.IsCancellationRequested && result.Count == sessions.Count)
                return result;

            state.Unassign(session, day, slot, classroom);
        }

        // Sin candidatos o timeout → devolver lo que tenemos (solución parcial)
        return [.. state.Assigned];
    }

    // ── Candidatos filtrados por hard constraints y ordenados por soft ────────

    private static List<(int Day, int Slot, Guid ClassroomId)> GetCandidateSlots(
        SessionToAssign session,
        AssignmentState state,
        GenerationContext context)
    {
        var candidates = new List<(int Day, int Slot, Guid ClassroomId, int Penalty)>();

        for (int day = 1; day <= 5; day++)
        {
            for (int slot = 0; slot < context.School.SlotsPerDay; slot++)
            {
                if (state.IsTeacherBusy(session.TeacherId, day, slot)) continue;

                var availableClassroom = state.FindAvailableClassroom(
                    day, slot, session.RequiredClassroomType, context.School.Classrooms);

                if (availableClassroom is null) continue;

                // Validar hard constraints
                var testEntry = new ProposedEntry(session, day, slot, availableClassroom.Value);
                bool hardViolation = context.HardConstraints
                    .Any(c => !c.IsSatisfied(testEntry, state));

                if (hardViolation) continue;

                // Calcular penalización de soft constraints
                int penalty = context.SoftConstraints
                    .Sum(c => c.Penalty(testEntry, state));

                candidates.Add((day, slot, availableClassroom.Value, penalty));
            }
        }

        // Menor penalización primero
        return [.. candidates
            .OrderBy(c => c.Penalty)
            .Select(c => (c.Day, c.Slot, c.ClassroomId))];
    }

    // ── Generación de explicaciones de conflictos ─────────────────────────────

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

// ── Estado mutable durante el backtracking ────────────────────────────────────

internal sealed class AssignmentState
{
    private readonly HashSet<(Guid TeacherId, int Day, int Slot)> _teacherSlots = [];
    private readonly HashSet<(Guid ClassroomId, int Day, int Slot)> _classroomSlots = [];
    private readonly List<AssignedSlot> _assigned = [];

    public IReadOnlyList<AssignedSlot> Assigned => _assigned;
    private readonly SchoolConfig _school;

    public AssignmentState(SchoolConfig school) => _school = school;

    public bool IsTeacherBusy(Guid teacherId, int day, int slot)
        => _teacherSlots.Contains((teacherId, day, slot));

    public Guid? FindAvailableClassroom(
        int day, int slot,
        ClassroomType? requiredType,
        IReadOnlyList<ClassroomInfo> classrooms)
    {
        var matching = classrooms
            .Where(c => requiredType is null
                ? c.Type == ClassroomType.Regular
                : c.Type == requiredType)
            .Where(c => !_classroomSlots.Contains((c.Id, day, slot)));

        return matching.FirstOrDefault()?.Id;
    }

    public void Assign(SessionToAssign session, int day, int slot, Guid classroomId)
    {
        _teacherSlots.Add((session.TeacherId, day, slot));
        _classroomSlots.Add((classroomId, day, slot));
        _assigned.Add(new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, classroomId, day, slot));
    }

    public void Unassign(SessionToAssign session, int day, int slot, Guid classroomId)
    {
        _teacherSlots.Remove((session.TeacherId, day, slot));
        _classroomSlots.Remove((classroomId, day, slot));
        _assigned.RemoveAll(a => a.AssignmentId == session.AssignmentId
                               && a.DayOfWeek == day
                               && a.SlotIndex == slot);
    }
}

// ── Tipos auxiliares ──────────────────────────────────────────────────────────

public record SchoolConfig(
    int SlotsPerDay,
    IReadOnlyList<ClassroomInfo> Classrooms);

public record ClassroomInfo(Guid Id, string Name, ClassroomType Type);
public record ProposedEntry(SessionToAssign Session, int Day, int Slot, Guid ClassroomId);
