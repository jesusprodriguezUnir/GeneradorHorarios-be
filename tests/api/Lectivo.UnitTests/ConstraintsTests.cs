using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class ConstraintsTests
{
    private readonly AssignmentState _state = new(TestData.DefaultSchool());

    [Fact]
    public void TeacherNotDoubleBooked_Satisfied_WhenTeacherFree()
    {
        var constraint = new TeacherNotDoubleBooked();
        var entry = new ProposedEntry(TestData.Session(), 1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void TeacherNotDoubleBooked_NotSatisfied_WhenTeacherBusy()
    {
        var session = TestData.Session();
        _state.Assign(session, day: 1, slot: 0, classroomId: TestData.RegularClassroomId);

        var constraint = new TeacherNotDoubleBooked();
        var entry = new ProposedEntry(TestData.Session(assignmentId: Guid.NewGuid()), 1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void ClassroomNotDoubleBooked_Satisfied_WhenClassroomFree()
    {
        var constraint = new ClassroomNotDoubleBooked();
        var entry = new ProposedEntry(TestData.Session(), 1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void ClassroomNotDoubleBooked_NotSatisfied_WhenClassroomBusy()
    {
        var session = TestData.Session();
        _state.Assign(session, day: 1, slot: 0, classroomId: TestData.RegularClassroomId);

        var constraint = new ClassroomNotDoubleBooked();
        var entry = new ProposedEntry(TestData.Session(assignmentId: Guid.NewGuid()), 1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void TeacherAvailability_Satisfied_WhenSlotNotUnavailable()
    {
        var constraint = new TeacherAvailabilityConstraint([]);
        var entry = new ProposedEntry(TestData.Session(), 1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void TeacherAvailability_NotSatisfied_WhenSlotUnavailable()
    {
        var teacher = TestData.Teacher1Id;
        var constraint = new TeacherAvailabilityConstraint([(teacher, 1, 0)]);
        var entry = new ProposedEntry(TestData.Session(teacherId: teacher), 1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void MaxConsecutiveSlots_Satisfied_BelowLimit()
    {
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;
        // Sin sesiones previas → 1 consecutiva, por debajo del límite 2
        var constraint = new MaxConsecutiveSlotsConstraint();
        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group, maxConsecutiveSlots: 2),
            1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void MaxConsecutiveSlots_NotSatisfied_ExceedsLimit()
    {
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;
        // Ya hay 2 sesiones consecutivas
        _state.Assign(TestData.Session(allocationId: allocation, groupId: group), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);
        _state.Assign(TestData.Session(allocationId: allocation, groupId: group), day: 1, slot: 1, classroomId: TestData.RegularClassroomId);

        var constraint = new MaxConsecutiveSlotsConstraint();
        // maxConsecutiveSlots = 2, ya hay 2 consecutivas → la tercera viola
        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group, maxConsecutiveSlots: 2),
            1, 2, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void NoIntensiveSubjectLastSlot_PenaltyZero_WhenNotLastSlot()
    {
        var intensiveId = TestData.Allocation1Id;
        var constraint = new NoIntensiveSubjectLastSlot(lastSlotIndex: 4, [intensiveId]);
        var entry = new ProposedEntry(TestData.Session(allocationId: intensiveId), 1, 2, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().Be(0);
    }

    [Fact]
    public void NoIntensiveSubjectLastSlot_PenaltyPositive_WhenLastSlot()
    {
        var intensiveId = TestData.Allocation1Id;
        var constraint = new NoIntensiveSubjectLastSlot(lastSlotIndex: 4, [intensiveId]);
        var entry = new ProposedEntry(TestData.Session(allocationId: intensiveId), 1, 4, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().BeGreaterThan(0);
    }

    [Fact]
    public void DistributeSubjectAcrossDays_PenaltyZero_WhenFirstSessionOfDay()
    {
        var constraint = new DistributeSubjectAcrossDays();
        var entry = new ProposedEntry(TestData.Session(), 1, 0, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().Be(0);
    }

    [Fact]
    public void DistributeSubjectAcrossDays_PenaltyPositive_WhenThirdSessionSameDay()
    {
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;
        _state.Assign(TestData.Session(allocationId: allocation, groupId: group), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);
        _state.Assign(TestData.Session(allocationId: allocation, groupId: group), day: 1, slot: 2, classroomId: TestData.RegularClassroomId);

        var constraint = new DistributeSubjectAcrossDays();
        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group),
            1, 3, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().BeGreaterThan(0);
    }

    [Fact]
    public void TeacherConsecutiveLoad_PenaltyZero_WhenBelowThreshold()
    {
        var constraint = new TeacherConsecutiveLoadConstraint(maxPreferred: 3);
        var entry = new ProposedEntry(TestData.Session(), 1, 0, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().Be(0);
    }

    [Fact]
    public void TeacherConsecutiveLoad_PenaltyPositive_WhenAtThreshold()
    {
        var teacher = TestData.Teacher1Id;
        _state.Assign(TestData.Session(teacherId: teacher), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);
        _state.Assign(TestData.Session(teacherId: teacher), day: 1, slot: 1, classroomId: TestData.RegularClassroomId);
        _state.Assign(TestData.Session(teacherId: teacher), day: 1, slot: 2, classroomId: TestData.RegularClassroomId);

        var constraint = new TeacherConsecutiveLoadConstraint(maxPreferred: 3);
        var entry = new ProposedEntry(
            TestData.Session(teacherId: teacher),
            1, 3, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().BeGreaterThan(0);
    }

    [Fact]
    public void RequiresSpecialistConstraint_Satisfied_WhenNotRequired()
    {
        var constraint = new RequiresSpecialistConstraint();
        var entry = new ProposedEntry(
            TestData.Session(requiresSpecialist: false, teacherSpecialties: ["Generalista"]),
            1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void RequiresSpecialistConstraint_Satisfied_WhenTeacherHasSpecialty()
    {
        var constraint = new RequiresSpecialistConstraint();
        var entry = new ProposedEntry(
            TestData.Session(requiresSpecialist: true, subjectKey: "ing", teacherSpecialties: ["Inglés"]),
            1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void RequiresSpecialistConstraint_NotSatisfied_WhenTeacherLacksSpecialty()
    {
        var constraint = new RequiresSpecialistConstraint();
        var entry = new ProposedEntry(
            TestData.Session(requiresSpecialist: true, subjectKey: "mus", teacherSpecialties: ["Generalista"]),
            1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void MaxWeeklyHoursConstraint_Satisfied_BelowLimit()
    {
        var constraint = new MaxWeeklyHoursConstraint();
        var teacher = TestData.Teacher1Id;
        var entry = new ProposedEntry(
            TestData.Session(teacherId: teacher, teacherMaxWeeklyHours: 5),
            1, 0, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeTrue();
    }

    [Fact]
    public void MaxWeeklyHoursConstraint_NotSatisfied_AtLimit()
    {
        var constraint = new MaxWeeklyHoursConstraint();
        var teacher = TestData.Teacher1Id;

        var session1 = TestData.Session(teacherId: teacher, teacherMaxWeeklyHours: 2);
        var session2 = TestData.Session(teacherId: teacher, teacherMaxWeeklyHours: 2);
        _state.Assign(session1, 1, 0, TestData.RegularClassroomId);
        _state.Assign(session2, 1, 1, TestData.RegularClassroomId);

        var entry = new ProposedEntry(
            TestData.Session(teacherId: teacher, teacherMaxWeeklyHours: 2),
            1, 2, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void TeacherGaps_PenaltyZero_WhenNoGaps()
    {
        var constraint = new TeacherGapsConstraint();
        var teacher = TestData.Teacher1Id;
        var entry = new ProposedEntry(TestData.Session(teacherId: teacher), 1, 0, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().Be(0);
    }

    [Fact]
    public void TeacherGaps_PenaltyPositive_WhenGapsExist()
    {
        var constraint = new TeacherGapsConstraint();
        var teacher = TestData.Teacher1Id;

        _state.Assign(TestData.Session(teacherId: teacher), 1, 0, TestData.RegularClassroomId);
        var entry = new ProposedEntry(TestData.Session(teacherId: teacher), 1, 2, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().BeGreaterThan(0);
    }

    [Fact]
    public void MaxConsecutiveSlots_NotSatisfied_ExceedsLimit_Bidirectional()
    {
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;

        _state.Assign(TestData.Session(allocationId: allocation, groupId: group), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);
        _state.Assign(TestData.Session(allocationId: allocation, groupId: group), day: 1, slot: 2, classroomId: TestData.RegularClassroomId);

        var constraint = new MaxConsecutiveSlotsConstraint();
        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group, maxConsecutiveSlots: 2),
            1, 1, TestData.RegularClassroomId);

        constraint.IsSatisfied(entry, _state).Should().BeFalse();
    }

    [Fact]
    public void ConsecutiveBlockPreference_PenaltyZero_WhenSplittable()
    {
        var constraint = new ConsecutiveBlockPreferenceConstraint();
        var entry = new ProposedEntry(
            TestData.Session(splittableAcrossDays: true),
            1, 0, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().Be(0);
    }

    [Fact]
    public void ConsecutiveBlockPreference_PenaltyPositive_WhenSplitAcrossDays()
    {
        var constraint = new ConsecutiveBlockPreferenceConstraint();
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;

        _state.Assign(TestData.Session(allocationId: allocation, groupId: group, splittableAcrossDays: false), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);

        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group, splittableAcrossDays: false),
            2, 0, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConsecutiveBlockPreference_PenaltyPositive_WhenSameDayNotConsecutive()
    {
        var constraint = new ConsecutiveBlockPreferenceConstraint();
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;

        _state.Assign(TestData.Session(allocationId: allocation, groupId: group, splittableAcrossDays: false), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);

        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group, splittableAcrossDays: false),
            1, 2, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConsecutiveBlockPreference_PenaltyZero_WhenSameDayConsecutive()
    {
        var constraint = new ConsecutiveBlockPreferenceConstraint();
        var allocation = TestData.Allocation1Id;
        var group = TestData.Group1Id;

        _state.Assign(TestData.Session(allocationId: allocation, groupId: group, splittableAcrossDays: false), day: 1, slot: 0, classroomId: TestData.RegularClassroomId);

        var entry = new ProposedEntry(
            TestData.Session(allocationId: allocation, groupId: group, splittableAcrossDays: false),
            1, 1, TestData.RegularClassroomId);

        constraint.Penalty(entry, _state).Should().Be(0);
    }
}
