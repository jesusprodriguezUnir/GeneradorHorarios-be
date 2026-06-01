using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Engine;

namespace HorariosEscolares.Domain.Services;

// ── Interfaz del motor ────────────────────────────────────────────────────────

public interface IScheduleEngine
{
    Task<ScheduleResult> GenerateAsync(
        GenerationContext context,
        CancellationToken cancellationToken,
        IProgress<GenerationProgress>? progress = null);
}

// ── Modelos de entrada/salida ─────────────────────────────────────────────────

/// <summary>
/// Pesos de las soft constraints (1-10). Valores por defecto coinciden con los
/// pesos originales hardcodeados. Permite que cada colegio personalice la
/// importancia relativa de cada criterio pedagógico.
/// </summary>
public record SoftConstraintWeights
{
    /// <summary>Penaliza materias intensivas (Mates, Lengua) en el último tramo horario.</summary>
    public int NoIntensiveSubjectLastSlot { get; init; } = 7;
    /// <summary>Penaliza acumular muchas sesiones de la misma asignatura en el mismo día.</summary>
    public int DistributeSubjectAcrossDays { get; init; } = 5;
    /// <summary>Penaliza más de N sesiones consecutivas para un mismo profesor.</summary>
    public int TeacherConsecutiveLoad { get; init; } = 6;
    /// <summary>Penaliza huecos intermedios en el horario diario del docente.</summary>
    public int TeacherGaps { get; init; } = 4;
    /// <summary>Penaliza dividir bloques indivisibles en días distintos o no consecutivos.</summary>
    public int ConsecutiveBlockPreference { get; init; } = 5;
}

public record GenerationContext
{
    public required SchoolConfig School { get; init; }
    public required IReadOnlyList<SessionToAssign> Sessions { get; init; }
    public required IReadOnlyList<IHardConstraint> HardConstraints { get; init; }
    public required IReadOnlyList<ISoftConstraint> SoftConstraints { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    /// <summary>Pesos de las soft constraints; si no se especifica se usan los defaults pedagógicos.</summary>
    public SoftConstraintWeights Weights { get; init; } = new();
}

/// <summary>Una sesión = una hora de una asignatura para un grupo.</summary>
public record SessionToAssign(
    Guid AssignmentId,
    Guid GroupId,
    Guid TeacherId,
    Guid AllocationId,
    string SubjectName,
    string GroupLabel,
    Guid? RequiredClassroomId,       // null = cualquier aula regular
    ClassroomType? RequiredClassroomType,
    int MaxConsecutiveSlots,
    bool RequiresSpecialist,
    IReadOnlyList<string> TeacherSpecialties,
    string SubjectKey,
    int TeacherMaxWeeklyHours,
    bool SplittableAcrossDays);

public record ScheduleResult
{
    public required GenerationStatus Status { get; init; }
    public required IReadOnlyList<AssignedSlot> AssignedSlots { get; init; }
    public required IReadOnlyList<ConflictExplanation> Conflicts { get; init; }
    public int ElapsedSeconds { get; init; }
    public int TotalAssigned => AssignedSlots.Count;
    public int TotalRequired { get; init; }
    /// <summary>
    /// Coste global calculado por <see cref="BacktrackingScheduleEngine.ComputeScheduleCost"/>.
    /// Es la suma de penalizaciones soft sobre el horario completo. Menor es mejor.
    /// Cero para resultados Failed (sin slots asignados).
    /// </summary>
    public int TotalCost { get; init; }
}

public record AssignedSlot(
    Guid AssignmentId,
    Guid GroupId,
    Guid TeacherId,
    Guid AllocationId,
    Guid ClassroomId,
    int DayOfWeek,
    int SlotIndex);

public record ConflictExplanation
{
    public required ConflictType Type { get; init; }
    public required ConflictSeverity Severity { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Suggestions { get; init; } = [];
    public Guid? GroupId { get; init; }
    public Guid? TeacherId { get; init; }
    public int? DayOfWeek { get; init; }
    public int? SlotIndex { get; init; }
}

public record GenerationProgress(
    int Assigned,
    int Total,
    string CurrentAction)
{
    public int Percentage => Total == 0 ? 0 : Assigned * 100 / Total;
}

public enum GenerationStatus { Complete, Partial, Failed }
public enum ClassroomType { Regular, Gym, Music, Lab, IT, Support }
