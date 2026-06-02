using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Engine;

/// <summary>
/// Motor de generación de horarios por backtracking con propagación de constraints.
/// Las sesiones más difíciles de asignar se intentan primero (fail-first heuristic).
/// </summary>
public sealed class BacktrackingScheduleEngine : IScheduleEngine
{
    private class BestSolutionTracker
    {
        private readonly object _lock = new();
        public List<AssignedSlot> Best { get; set; } = [];

        public void Update(IReadOnlyList<AssignedSlot> current)
        {
            lock (_lock)
            {
                if (current.Count > Best.Count)
                {
                    Best = [.. current];
                }
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
            () => Backtrack(sessions, 0, state, context, timeoutCts.Token, progress, tracker),
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

    // ── Heurística: sesiones más restringidas primero ─────────────────────────

    private static List<SessionToAssign> PrioritizeSessions(
        IReadOnlyList<SessionToAssign> sessions)
    {
        var teacherTotalHours = sessions
            .GroupBy(s => s.TeacherId)
            .ToDictionary(g => g.Key, g => g.Count());

        return [.. sessions.OrderByDescending(s =>
        {
            int score = 0;
            if (s.RequiredClassroomType is not null) score += 100; // aula especial → más difícil
            if (s.MaxConsecutiveSlots == 1) score += 50;           // no puede ir consecutiva
            if (!s.SplittableAcrossDays) score += 40;              // bloque indivisible → difícil

            // Priorizar profesores con mucha carga semanal (más restrictivos)
            int tHours = teacherTotalHours.GetValueOrDefault(s.TeacherId, 0);
            score += tHours * 2;

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
        IProgress<GenerationProgress>? progress,
        BestSolutionTracker tracker)
    {
        if (ct.IsCancellationRequested)
            return tracker.Best;

        if (index >= sessions.Count)
            return [.. state.Assigned];

        var session = sessions[index];
        var candidates = GetCandidateSlots(session, state, context);

        foreach (var (day, slot, classroom) in candidates)
        {
            if (ct.IsCancellationRequested) break;

            state.Assign(session, day, slot, classroom);
            tracker.Update(state.Assigned);

            progress?.Report(new GenerationProgress(
                state.Assigned.Count,
                sessions.Count,
                $"Asignando {session.SubjectName} a {session.GroupLabel}..."));

            var result = Backtrack(sessions, index + 1, state, context, ct, progress, tracker);

            // Si llegamos al final sin cancelar, éxito total
            if (!ct.IsCancellationRequested && result.Count == sessions.Count)
                return result;

            state.Unassign(session, day, slot, classroom);
        }

        // Sin candidatos o timeout → devolver la mejor solución parcial encontrada
        return tracker.Best;
    }

    // ── Candidatos filtrados por hard constraints y ordenados por soft ────────

    private static List<(int Day, int Slot, Guid ClassroomId)> GetCandidateSlots(
        SessionToAssign session,
        AssignmentState state,
        GenerationContext context)
    {
        var candidates = new List<(int Day, int Slot, Guid ClassroomId, int Penalty)>();

        foreach (var day in context.School.WorkingDays)
        {
            foreach (var slotConfig in context.School.Slots)
            {
                if (slotConfig.IsBreak) continue;
                int slot = slotConfig.Index;

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

    // ── Función objetivo global: coste total del horario ─────────────────────

    /// <summary>
    /// Calcula el coste global del horario sumando las penalizaciones soft sobre
    /// todos los slots asignados. Reproduce la evaluación incremental del motor:
    /// evalúa la penalización de cada slot en el estado parcial anterior a
    /// asignarlo, igual que hace <see cref="GetCandidateSlots"/>.
    /// Menor valor = mejor calidad pedagógica. 0 = sin penalizaciones.
    /// </summary>
    public static int ComputeScheduleCost(
        IReadOnlyList<AssignedSlot> schedule,
        IReadOnlyList<SessionToAssign> sessions,
        IReadOnlyList<ISoftConstraint> softConstraints)
    {
        if (schedule.Count == 0 || softConstraints.Count == 0) return 0;

        // Índice rápido: múltiples sesiones comparten AssignmentId (una por hora semanal)
        var sessionIndex = sessions
            .GroupBy(s => s.AssignmentId)
            .ToDictionary(g => g.Key, g => g.First());

        var state = new AssignmentState(
            // SchoolConfig no interviene en los soft constraints actuales; usamos
            // un config mínimo para inicializar el estado vacío correctamente.
            new SchoolConfig(0, 0, [], [], []));

        int totalCost = 0;

        foreach (var slot in schedule)
        {
            if (!sessionIndex.TryGetValue(slot.AssignmentId, out var session))
                continue;

            var entry = new ProposedEntry(session, slot.DayOfWeek, slot.SlotIndex, slot.ClassroomId);

            // Evalúa penalización ANTES de asignar (coherente con GetCandidateSlots)
            totalCost += softConstraints.Sum(c => c.Penalty(entry, state));

            state.Assign(session, slot.DayOfWeek, slot.SlotIndex, slot.ClassroomId);
        }

        return totalCost;
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

public sealed class AssignmentState
{
    private readonly HashSet<(Guid TeacherId, int Day, int Slot)> _teacherSlots = [];
    private readonly HashSet<(Guid ClassroomId, int Day, int Slot)> _classroomSlots = [];
    private readonly Dictionary<Guid, int> _teacherAssignedHours = [];
    private readonly List<AssignedSlot> _assigned = [];

    public IReadOnlyList<AssignedSlot> Assigned => _assigned;
    private readonly SchoolConfig _school;

    public AssignmentState(SchoolConfig school) => _school = school;

    public bool IsTeacherBusy(Guid teacherId, int day, int slot)
        => _teacherSlots.Contains((teacherId, day, slot));

    public bool IsClassroomBusy(Guid classroomId, int day, int slot)
        => _classroomSlots.Contains((classroomId, day, slot));

    public int GetTeacherAssignedHours(Guid teacherId)
        => _teacherAssignedHours.GetValueOrDefault(teacherId, 0);

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
        _teacherAssignedHours[session.TeacherId] = _teacherAssignedHours.GetValueOrDefault(session.TeacherId, 0) + 1;
        _assigned.Add(new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, classroomId, day, slot));
    }

    public void Unassign(SessionToAssign session, int day, int slot, Guid classroomId)
    {
        _teacherSlots.Remove((session.TeacherId, day, slot));
        _classroomSlots.Remove((classroomId, day, slot));
        _teacherAssignedHours[session.TeacherId] = Math.Max(0, _teacherAssignedHours.GetValueOrDefault(session.TeacherId, 0) - 1);
        _assigned.RemoveAll(a => a.AssignmentId == session.AssignmentId
                               && a.DayOfWeek == day
                               && a.SlotIndex == slot);
    }
}

// ── Tipos auxiliares ──────────────────────────────────────────────────────────

public record SlotConfig(int Index, bool IsBreak);

public record SchoolConfig(
    int SlotsPerDay,
    int DaysPerWeek,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<SlotConfig> Slots,
    IReadOnlyList<ClassroomInfo> Classrooms);

public record ClassroomInfo(Guid Id, string Name, ClassroomType Type);
public record ProposedEntry(SessionToAssign Session, int Day, int Slot, Guid ClassroomId);
