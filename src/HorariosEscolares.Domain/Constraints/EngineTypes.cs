using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Domain.Constraints;

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

public record SlotConfig(int Index, bool IsBreak);

public record SchoolConfig(
    int SlotsPerDay,
    int DaysPerWeek,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<SlotConfig> Slots,
    IReadOnlyList<ClassroomInfo> Classrooms);

public record ClassroomInfo(Guid Id, string Name, ClassroomType Type);

public record ProposedEntry(SessionToAssign Session, int Day, int Slot, Guid ClassroomId);
