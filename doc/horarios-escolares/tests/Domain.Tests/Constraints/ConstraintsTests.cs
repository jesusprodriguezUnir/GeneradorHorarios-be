using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Infrastructure.Engine;

namespace HorariosEscolares.Domain.Tests.Constraints;

/// <summary>
/// Tests de constraints — cobertura 100% obligatoria (ver Constitución Art. 4.1)
/// </summary>
public class TeacherNotDoubleBookedTests
{
    private readonly TeacherNotDoubleBooked _constraint = new();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _allocationId = Guid.NewGuid();

    [Fact]
    public void IsSatisfied_WhenTeacherFree_ReturnsTrue()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var entry = BuildEntry(_teacherId, day: 1, slot: 0);

        Assert.True(_constraint.IsSatisfied(entry, state));
    }

    [Fact]
    public void IsSatisfied_WhenTeacherBusySameDaySlot_ReturnsFalse()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var session = BuildSession(_teacherId);

        state.Assign(session, day: 1, slot: 0, classroomId: Guid.NewGuid());
        var entry = BuildEntry(_teacherId, day: 1, slot: 0);

        Assert.False(_constraint.IsSatisfied(entry, state));
    }

    [Fact]
    public void IsSatisfied_WhenTeacherBusyDifferentSlot_ReturnsTrue()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var session = BuildSession(_teacherId);

        state.Assign(session, day: 1, slot: 0, classroomId: Guid.NewGuid());
        var entry = BuildEntry(_teacherId, day: 1, slot: 1); // slot diferente

        Assert.True(_constraint.IsSatisfied(entry, state));
    }

    [Fact]
    public void IsSatisfied_WhenTeacherBusyDifferentDay_ReturnsTrue()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var session = BuildSession(_teacherId);

        state.Assign(session, day: 1, slot: 0, classroomId: Guid.NewGuid());
        var entry = BuildEntry(_teacherId, day: 2, slot: 0); // día diferente

        Assert.True(_constraint.IsSatisfied(entry, state));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SessionToAssign BuildSession(Guid teacherId) => new(
        AssignmentId: Guid.NewGuid(),
        GroupId: _groupId,
        TeacherId: teacherId,
        AllocationId: _allocationId,
        SubjectName: "Matemáticas",
        GroupLabel: "3ºA",
        RequiredClassroomId: null,
        RequiredClassroomType: null,
        MaxConsecutiveSlots: 2);

    private ProposedEntry BuildEntry(Guid teacherId, int day, int slot) => new(
        Session: BuildSession(teacherId),
        Day: day,
        Slot: slot,
        ClassroomId: Guid.NewGuid());
}

public class TeacherAvailabilityConstraintTests
{
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void IsSatisfied_WhenSlotNotInUnavailable_ReturnsTrue()
    {
        var constraint = new TeacherAvailabilityConstraint(
            [(_teacherId, Day: 1, Slot: 2)] // martes slot 2 no disponible
        );
        var state = new AssignmentState(new SchoolConfig(6, []));
        var entry = BuildEntry(_teacherId, day: 1, slot: 0); // slot diferente

        Assert.True(constraint.IsSatisfied(entry, state));
    }

    [Fact]
    public void IsSatisfied_WhenSlotInUnavailable_ReturnsFalse()
    {
        var constraint = new TeacherAvailabilityConstraint(
            [(_teacherId, Day: 1, Slot: 0)]
        );
        var state = new AssignmentState(new SchoolConfig(6, []));
        var entry = BuildEntry(_teacherId, day: 1, slot: 0);

        Assert.False(constraint.IsSatisfied(entry, state));
    }

    private ProposedEntry BuildEntry(Guid teacherId, int day, int slot) => new(
        Session: new SessionToAssign(
            Guid.NewGuid(), Guid.NewGuid(), teacherId, Guid.NewGuid(),
            "Inglés", "2ºA", null, null, 2),
        Day: day, Slot: slot, ClassroomId: Guid.NewGuid());
}

public class DistributeSubjectAcrossDaysTests
{
    private readonly DistributeSubjectAcrossDays _constraint = new();
    private readonly Guid _allocationId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void Penalty_WhenNoSameSubjectSameDay_ReturnsZero()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var entry = BuildEntry(day: 1, slot: 0);

        Assert.Equal(0, _constraint.Penalty(entry, state));
    }

    [Fact]
    public void Penalty_WhenOneSameSubjectSameDay_ReturnsPositive()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var session = BuildSession();
        state.Assign(session, day: 1, slot: 0, Guid.NewGuid());

        var entry = BuildEntry(day: 1, slot: 1);

        Assert.True(_constraint.Penalty(entry, state) > 0);
    }

    [Fact]
    public void Penalty_WhenTwoSameSubjectSameDay_ReturnsHigherPenalty()
    {
        var state = new AssignmentState(new SchoolConfig(6, []));
        var session = BuildSession();
        state.Assign(session, day: 1, slot: 0, Guid.NewGuid());
        state.Assign(session, day: 1, slot: 1, Guid.NewGuid());

        int penaltyOne = _constraint.Penalty(BuildEntry(day: 1, slot: 1), state);
        int penaltyTwo = _constraint.Penalty(BuildEntry(day: 1, slot: 2), state);

        Assert.True(penaltyTwo > penaltyOne);
    }

    private SessionToAssign BuildSession() => new(
        Guid.NewGuid(), _groupId, _teacherId, _allocationId,
        "Matemáticas", "3ºA", null, null, 2);

    private ProposedEntry BuildEntry(int day, int slot) => new(
        BuildSession(), day, slot, Guid.NewGuid());
}
