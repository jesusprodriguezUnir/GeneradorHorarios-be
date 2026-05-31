using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Domain.Constraints;

// ── Interfaz base ─────────────────────────────────────────────────────────────

public interface IHardConstraint
{
    string Name { get; }
    bool IsSatisfied(ProposedEntry entry, AssignmentState state);
}

public interface ISoftConstraint
{
    string Name { get; }
    int Weight { get; }  // 1-10
    int Penalty(ProposedEntry entry, AssignmentState state);
}

// ── Hard constraints ──────────────────────────────────────────────────────────

/// <summary>Un profesor no puede estar en dos grupos al mismo tiempo.</summary>
public sealed class TeacherNotDoubleBooked : IHardConstraint
{
    public string Name => "Profesor no duplicado";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => !state.IsTeacherBusy(entry.Session.TeacherId, entry.Day, entry.Slot);
}

/// <summary>Un aula no puede tener dos grupos al mismo tiempo.</summary>
public sealed class ClassroomNotDoubleBooked : IHardConstraint
{
    public string Name => "Aula no duplicada";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => !state.IsClassroomBusy(entry.ClassroomId, entry.Day, entry.Slot);
}

/// <summary>Respetar la no-disponibilidad declarada de un profesor.</summary>
public sealed class TeacherAvailabilityConstraint : IHardConstraint
{
    private readonly HashSet<(Guid TeacherId, int Day, int Slot)> _unavailable;

    public string Name => "Disponibilidad del profesor";

    public TeacherAvailabilityConstraint(
        IEnumerable<(Guid TeacherId, int Day, int Slot)> unavailableSlots)
    {
        _unavailable = [.. unavailableSlots];
    }

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => !_unavailable.Contains((entry.Session.TeacherId, entry.Day, entry.Slot));
}

/// <summary>No superar el máximo de sesiones consecutivas de una asignatura.</summary>
public sealed class MaxConsecutiveSlotsConstraint : IHardConstraint
{
    public string Name => "Máximo sesiones consecutivas";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
    {
        if (entry.Session.MaxConsecutiveSlots <= 1) return true;

        int consecutive = CountConsecutive(
            state.Assigned, entry.Session.AllocationId,
            entry.Session.GroupId, entry.Day, entry.Slot);

        return consecutive < entry.Session.MaxConsecutiveSlots;
    }

    private static int CountConsecutive(
        IReadOnlyList<AssignedSlot> assigned,
        Guid allocationId, Guid groupId, int day, int slot)
    {
        int count = 1;

        // Contar hacia atrás
        int check = slot - 1;
        while (assigned.Any(a => a.AllocationId == allocationId
                               && a.GroupId == groupId
                               && a.DayOfWeek == day
                               && a.SlotIndex == check))
        {
            count++;
            check--;
        }

        return count;
    }
}

// ── Soft constraints ──────────────────────────────────────────────────────────

/// <summary>Penaliza poner asignaturas intensivas (Mates, Lengua) en el último tramo.</summary>
public sealed class NoIntensiveSubjectLastSlot : ISoftConstraint
{
    private readonly int _lastSlotIndex;
    private readonly HashSet<Guid> _intensiveAllocationIds;

    public string Name => "Asignatura intensiva no en último tramo";
    public int Weight => 7;

    public NoIntensiveSubjectLastSlot(int lastSlotIndex, IEnumerable<Guid> intensiveIds)
    {
        _lastSlotIndex = lastSlotIndex;
        _intensiveAllocationIds = [.. intensiveIds];
    }

    public int Penalty(ProposedEntry entry, AssignmentState state)
        => entry.Slot == _lastSlotIndex
           && _intensiveAllocationIds.Contains(entry.Session.AllocationId)
            ? Weight * 10
            : 0;
}

/// <summary>Penaliza acumular muchas horas de la misma asignatura en el mismo día.</summary>
public sealed class DistributeSubjectAcrossDays : ISoftConstraint
{
    public string Name => "Distribuir asignatura entre días";
    public int Weight => 5;

    public int Penalty(ProposedEntry entry, AssignmentState state)
    {
        int sameSubjectSameDay = state.Assigned.Count(a =>
            a.AllocationId == entry.Session.AllocationId
            && a.GroupId == entry.Session.GroupId
            && a.DayOfWeek == entry.Day);

        return sameSubjectSameDay switch
        {
            0 => 0,
            1 => Weight * 5,
            _ => Weight * 20  // más de 2 sesiones del mismo día → penalización alta
        };
    }
}

/// <summary>Penaliza horarios con más de N sesiones consecutivas para un profesor.</summary>
public sealed class TeacherConsecutiveLoadConstraint : ISoftConstraint
{
    private readonly int _maxPreferred;

    public string Name => "Carga consecutiva del profesor";
    public int Weight => 6;

    public TeacherConsecutiveLoadConstraint(int maxPreferred = 3)
        => _maxPreferred = maxPreferred;

    public int Penalty(ProposedEntry entry, AssignmentState state)
    {
        int consecutive = state.Assigned.Count(a =>
            a.TeacherId == entry.Session.TeacherId
            && a.DayOfWeek == entry.Day
            && Math.Abs(a.SlotIndex - entry.Slot) <= _maxPreferred);

        return consecutive >= _maxPreferred ? Weight * 15 : 0;
    }
}

/// <summary>Evita asignar un profesor sin la especialidad requerida para la asignatura.</summary>
public sealed class RequiresSpecialistConstraint : IHardConstraint
{
    public string Name => "Especialidad requerida";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
    {
        if (!entry.Session.RequiresSpecialist) return true;

        var specialties = entry.Session.TeacherSpecialties;
        var key = entry.Session.SubjectKey.ToLower();

        if (key == "ing")
            return specialties.Any(s => s.Contains("Inglés", StringComparison.OrdinalIgnoreCase));
        if (key == "ef")
            return specialties.Any(s => s.Contains("Física", StringComparison.OrdinalIgnoreCase) || s.Contains("Deporte", StringComparison.OrdinalIgnoreCase));
        if (key == "mus")
            return specialties.Any(s => s.Contains("Música", StringComparison.OrdinalIgnoreCase));

        return specialties.Any(s => s.Contains("Generalista", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>No superar el límite de horas semanales de docencia de un profesor.</summary>
public sealed class MaxWeeklyHoursConstraint : IHardConstraint
{
    public string Name => "Límite horas semanales";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => state.GetTeacherAssignedHours(entry.Session.TeacherId) < entry.Session.TeacherMaxWeeklyHours;
}

/// <summary>Penaliza las ventanas libres intermedias (huecos) en el horario diario de un profesor.</summary>
public sealed class TeacherGapsConstraint : ISoftConstraint
{
    public string Name => "Evitar huecos profesor";
    public int Weight => 4;

    public int Penalty(ProposedEntry entry, AssignmentState state)
    {
        var slots = state.Assigned
            .Where(a => a.TeacherId == entry.Session.TeacherId && a.DayOfWeek == entry.Day)
            .Select(a => a.SlotIndex)
            .Append(entry.Slot)
            .OrderBy(s => s)
            .ToList();

        if (slots.Count <= 1) return 0;

        int gaps = 0;
        int min = slots[0];
        int max = slots[^1];

        for (int s = min + 1; s < max; s++)
        {
            if (!slots.Contains(s))
            {
                gaps++;
            }
        }

        return gaps * Weight * 5;
    }
}
