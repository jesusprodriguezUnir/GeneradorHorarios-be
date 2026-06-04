using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Domain.Constraints;

public interface IHardConstraint
{
    string Name { get; }
    bool IsSatisfied(ProposedEntry entry, AssignmentState state);
}

public interface ISoftConstraint
{
    string Name { get; }
    int Weight { get; }
    int Penalty(ProposedEntry entry, AssignmentState state);
}

public sealed class TeacherNotDoubleBooked : IHardConstraint
{
    public string Name => "Profesor no duplicado";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => !state.IsTeacherBusy(entry.Session.TeacherId, entry.Day, entry.Slot);
}

public sealed class ClassroomNotDoubleBooked : IHardConstraint
{
    public string Name => "Aula no duplicada";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => !state.IsClassroomBusy(entry.ClassroomId, entry.Day, entry.Slot);
}

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

        int checkBack = slot - 1;
        while (assigned.Any(a => a.AllocationId == allocationId
                               && a.GroupId == groupId
                               && a.DayOfWeek == day
                               && a.SlotIndex == checkBack))
        {
            count++;
            checkBack--;
        }

        int checkForward = slot + 1;
        while (assigned.Any(a => a.AllocationId == allocationId
                               && a.GroupId == groupId
                               && a.DayOfWeek == day
                               && a.SlotIndex == checkForward))
        {
            count++;
            checkForward++;
        }

        return count;
    }
}

public sealed class NoIntensiveSubjectLastSlot : ISoftConstraint
{
    private readonly int _lastSlotIndex;
    private readonly HashSet<Guid> _intensiveAllocationIds;

    public string Name => "Asignatura intensiva no en último tramo";
    public int Weight { get; }

    public NoIntensiveSubjectLastSlot(int lastSlotIndex, IEnumerable<Guid> intensiveIds, int weight = 7)
    {
        _lastSlotIndex = lastSlotIndex;
        _intensiveAllocationIds = [.. intensiveIds];
        Weight = weight;
    }

    public int Penalty(ProposedEntry entry, AssignmentState state)
        => entry.Slot == _lastSlotIndex
           && _intensiveAllocationIds.Contains(entry.Session.AllocationId)
            ? Weight * 10
            : 0;
}

public sealed class DistributeSubjectAcrossDays : ISoftConstraint
{
    public string Name => "Distribuir asignatura entre días";
    public int Weight { get; }

    public DistributeSubjectAcrossDays(int weight = 5) => Weight = weight;

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
            _ => Weight * 20
        };
    }
}

public sealed class TeacherConsecutiveLoadConstraint : ISoftConstraint
{
    private readonly int _maxPreferred;

    public string Name => "Carga consecutiva del profesor";
    public int Weight { get; }

    public TeacherConsecutiveLoadConstraint(int maxPreferred = 3, int weight = 6)
    {
        _maxPreferred = maxPreferred;
        Weight = weight;
    }

    public int Penalty(ProposedEntry entry, AssignmentState state)
    {
        int consecutive = state.Assigned.Count(a =>
            a.TeacherId == entry.Session.TeacherId
            && a.DayOfWeek == entry.Day
            && Math.Abs(a.SlotIndex - entry.Slot) <= _maxPreferred);

        return consecutive >= _maxPreferred ? Weight * 15 : 0;
    }
}

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

public sealed class MaxWeeklyHoursConstraint : IHardConstraint
{
    public string Name => "Límite horas semanales";

    public bool IsSatisfied(ProposedEntry entry, AssignmentState state)
        => state.GetTeacherAssignedHours(entry.Session.TeacherId) < entry.Session.TeacherMaxWeeklyHours;
}

public sealed class TeacherGapsConstraint : ISoftConstraint
{
    public string Name => "Evitar huecos profesor";
    public int Weight { get; }

    public TeacherGapsConstraint(int weight = 4) => Weight = weight;

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
            if (!slots.Contains(s)) gaps++;
        }

        return gaps * Weight * 5;
    }
}

public sealed class ConsecutiveBlockPreferenceConstraint : ISoftConstraint
{
    public string Name => "Preferencia de bloques consecutivos";
    public int Weight { get; }

    public ConsecutiveBlockPreferenceConstraint(int weight = 5) => Weight = weight;

    public int Penalty(ProposedEntry entry, AssignmentState state)
    {
        if (entry.Session.SplittableAcrossDays) return 0;

        var assignedOnOtherDays = state.Assigned
            .Any(a => a.AllocationId == entry.Session.AllocationId
                   && a.GroupId == entry.Session.GroupId
                   && a.DayOfWeek != entry.Day);

        if (assignedOnOtherDays) return Weight * 15;

        var assignedOnSameDay = state.Assigned
            .Where(a => a.AllocationId == entry.Session.AllocationId
                     && a.GroupId == entry.Session.GroupId
                     && a.DayOfWeek == entry.Day)
            .ToList();

        if (assignedOnSameDay.Count > 0)
        {
            bool isConsecutive = assignedOnSameDay.Any(a => Math.Abs(a.SlotIndex - entry.Slot) == 1);
            if (!isConsecutive) return Weight * 10;
        }

        return 0;
    }
}
