using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Domain.Constraints;

public sealed class AssignmentState
{
    private readonly HashSet<(Guid TeacherId, int Day, int Bucket)> _teacherBuckets = [];
    private readonly HashSet<(Guid ClassroomId, int Day, int Bucket)> _classroomBuckets = [];
    private readonly Dictionary<Guid, int> _teacherAssignedHours = [];
    private readonly List<AssignedSlot> _assigned = [];

    public IReadOnlyList<AssignedSlot> Assigned => _assigned;
    private readonly SchoolConfig _school;

    public AssignmentState(SchoolConfig school) => _school = school;

    private static IEnumerable<int> BucketRange(int startMinute, int endMinute)
    {
        int start = startMinute / 5;
        int end = (endMinute + 4) / 5;
        for (int b = start; b < end; b++)
            yield return b;
    }

    public bool IsTeacherBusy(Guid teacherId, int day, int startMinute, int endMinute)
        => BucketRange(startMinute, endMinute).Any(b => _teacherBuckets.Contains((teacherId, day, b)));

    public bool IsClassroomBusy(Guid classroomId, int day, int startMinute, int endMinute)
        => BucketRange(startMinute, endMinute).Any(b => _classroomBuckets.Contains((classroomId, day, b)));

    public int GetTeacherAssignedHours(Guid teacherId)
        => _teacherAssignedHours.GetValueOrDefault(teacherId, 0);

    public Guid? FindAvailableClassroom(
        int day, int startMinute, int endMinute,
        ClassroomType? requiredType,
        IReadOnlyList<ClassroomInfo> classrooms)
    {
        var matching = classrooms
            .Where(c => requiredType is null
                ? c.Type == ClassroomType.Regular
                : c.Type == requiredType)
            .Where(c => !BucketRange(startMinute, endMinute).Any(b => _classroomBuckets.Contains((c.Id, day, b))));

        return matching.FirstOrDefault()?.Id;
    }

    public void Assign(SessionToAssign session, int day, int slot, int startMinute, int endMinute, Guid classroomId)
    {
        foreach (var b in BucketRange(startMinute, endMinute))
        {
            _teacherBuckets.Add((session.TeacherId, day, b));
            _classroomBuckets.Add((classroomId, day, b));
        }
        _teacherAssignedHours[session.TeacherId] = _teacherAssignedHours.GetValueOrDefault(session.TeacherId, 0) + 1;
        _assigned.Add(new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, classroomId, day, slot, startMinute, endMinute, session.Cycle,
            session.SubjectKey));
    }

    public void Unassign(SessionToAssign session, int day, int slot, int startMinute, int endMinute, Guid classroomId)
    {
        foreach (var b in BucketRange(startMinute, endMinute))
        {
            _teacherBuckets.Remove((session.TeacherId, day, b));
            _classroomBuckets.Remove((classroomId, day, b));
        }
        _teacherAssignedHours[session.TeacherId] = Math.Max(0, _teacherAssignedHours.GetValueOrDefault(session.TeacherId, 0) - 1);
        _assigned.RemoveAll(a => a.AssignmentId == session.AssignmentId
                               && a.DayOfWeek == day
                               && a.SlotIndex == slot);
    }
}

public record SlotConfig(int Index, bool IsBreak, int StartMinute, int EndMinute);

public record CycleGrid(int Cycle, IReadOnlyList<SlotConfig> Slots);

public record SchoolConfig(
    int SlotsPerDay,
    int DaysPerWeek,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<CycleGrid> Cycles,
    IReadOnlyList<ClassroomInfo> Classrooms)
{
    public IReadOnlyList<SlotConfig> SlotsFor(int cycle)
        => Cycles.FirstOrDefault(c => c.Cycle == cycle)?.Slots ?? [];

    public SlotConfig? Slot(int cycle, int index)
        => SlotsFor(cycle).FirstOrDefault(s => s.Index == index);
}

public record ClassroomInfo(Guid Id, string Name, ClassroomType Type);

public record ProposedEntry(SessionToAssign Session, int Day, int Slot, Guid ClassroomId, int StartMinute, int EndMinute);
