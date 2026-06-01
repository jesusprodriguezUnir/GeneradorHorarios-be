using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class ComputeScheduleCostTests
{
    private static readonly SchoolConfig School = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5);

    // ── Coste cero ────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeScheduleCost_ReturnsZero_WhenNoSoftConstraints()
    {
        var session = TestData.Session();
        var slot = new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, TestData.RegularClassroomId,
            DayOfWeek: 1, SlotIndex: 0);

        var cost = BacktrackingScheduleEngine.ComputeScheduleCost([slot], [session], []);

        cost.Should().Be(0);
    }

    [Fact]
    public void ComputeScheduleCost_ReturnsZero_WhenEmptySchedule()
    {
        var softConstraints = new List<ISoftConstraint>
        {
            new DistributeSubjectAcrossDays()
        };

        var cost = BacktrackingScheduleEngine.ComputeScheduleCost([], [], softConstraints);

        cost.Should().Be(0);
    }

    // ── NoIntensiveSubjectLastSlot ─────────────────────────────────────────────

    [Fact]
    public void ComputeScheduleCost_ChargesPenalty_WhenIntensiveSubjectInLastSlot()
    {
        int lastSlot = 4; // índice del último slot (5 slots: 0-4)
        var allocationId = TestData.Allocation1Id;
        var session = TestData.Session(allocationId: allocationId, subjectKey: "mat", subjectName: "Matemáticas");
        int weight = 7;
        var constraint = new NoIntensiveSubjectLastSlot(lastSlot, [allocationId], weight);

        var slot = new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, TestData.RegularClassroomId,
            DayOfWeek: 1, SlotIndex: lastSlot);

        var cost = BacktrackingScheduleEngine.ComputeScheduleCost([slot], [session], [constraint]);

        cost.Should().Be(weight * 10);
    }

    [Fact]
    public void ComputeScheduleCost_NoPenalty_WhenIntensiveSubjectNotInLastSlot()
    {
        int lastSlot = 4;
        var allocationId = TestData.Allocation1Id;
        var session = TestData.Session(allocationId: allocationId, subjectKey: "mat");
        var constraint = new NoIntensiveSubjectLastSlot(lastSlot, [allocationId]);

        var slot = new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, TestData.RegularClassroomId,
            DayOfWeek: 1, SlotIndex: 0); // primer slot, no último

        var cost = BacktrackingScheduleEngine.ComputeScheduleCost([slot], [session], [constraint]);

        cost.Should().Be(0);
    }

    // ── Peso configurable afecta al coste ─────────────────────────────────────

    [Fact]
    public void ComputeScheduleCost_ScalesWithWeight_ForNoIntensiveLastSlot()
    {
        int lastSlot = 4;
        var allocationId = TestData.Allocation1Id;
        var session = TestData.Session(allocationId: allocationId, subjectKey: "mat");

        var slot = new AssignedSlot(
            session.AssignmentId, session.GroupId, session.TeacherId,
            session.AllocationId, TestData.RegularClassroomId,
            DayOfWeek: 1, SlotIndex: lastSlot);

        int weightLow  = 3;
        int weightHigh = 9;

        var costLow  = BacktrackingScheduleEngine.ComputeScheduleCost(
            [slot], [session], [new NoIntensiveSubjectLastSlot(lastSlot, [allocationId], weightLow)]);
        var costHigh = BacktrackingScheduleEngine.ComputeScheduleCost(
            [slot], [session], [new NoIntensiveSubjectLastSlot(lastSlot, [allocationId], weightHigh)]);

        costLow.Should().Be(weightLow * 10);
        costHigh.Should().Be(weightHigh * 10);
        costHigh.Should().BeGreaterThan(costLow);
    }

    // ── DistributeSubjectAcrossDays ───────────────────────────────────────────

    [Fact]
    public void ComputeScheduleCost_PenalizesDoubleSubjectSameDay()
    {
        var allocationId = Guid.NewGuid();
        var groupId = TestData.Group1Id;
        var session1 = TestData.Session(assignmentId: Guid.NewGuid(), allocationId: allocationId, groupId: groupId);
        var session2 = TestData.Session(assignmentId: Guid.NewGuid(), allocationId: allocationId, groupId: groupId);

        // Dos sesiones de la misma asignatura el mismo día
        var slot1 = new AssignedSlot(session1.AssignmentId, groupId, session1.TeacherId, allocationId, TestData.RegularClassroomId, 1, 0);
        var slot2 = new AssignedSlot(session2.AssignmentId, groupId, session2.TeacherId, allocationId, TestData.RegularClassroomId, 1, 2);

        int weight = 5;
        var constraint = new DistributeSubjectAcrossDays(weight);

        var cost = BacktrackingScheduleEngine.ComputeScheduleCost(
            [slot1, slot2], [session1, session2], [constraint]);

        // slot1: 0 sesiones previas → penalización 0
        // slot2: 1 sesión previa mismo día → penalización weight*5
        cost.Should().Be(weight * 5);
    }
}
