using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class AssignmentStateTests
{
    private readonly AssignmentState _state = new(TestData.DefaultSchool());

    [Fact]
    public void IsTeacherBusy_ReturnsFalse_WhenFree()
    {
        _state.IsTeacherBusy(TestData.Teacher1Id, day: 1, startMinute: 0, endMinute: 60).Should().BeFalse();
    }

    [Fact]
    public void IsTeacherBusy_ReturnsTrue_AfterAssign()
    {
        var session = TestData.Session();
        _state.Assign(session, day: 1, slot: 0, startMinute: 0, endMinute: 60, classroomId: TestData.RegularClassroomId);

        _state.IsTeacherBusy(session.TeacherId, day: 1, startMinute: 0, endMinute: 60).Should().BeTrue();
    }

    [Fact]
    public void FindAvailableClassroom_ReturnsRegular_WhenNoRequirement()
    {
        var id = _state.FindAvailableClassroom(day: 1, startMinute: 0, endMinute: 60, requiredType: null,
            TestData.DefaultSchool().Classrooms);

        id.Should().Be(TestData.RegularClassroomId);
    }

    [Fact]
    public void FindAvailableClassroom_ReturnsGym_WhenRequired()
    {
        var id = _state.FindAvailableClassroom(day: 1, startMinute: 0, endMinute: 60, requiredType: ClassroomType.Gym,
            TestData.DefaultSchool().Classrooms);

        id.Should().Be(TestData.GymClassroomId);
    }

    [Fact]
    public void FindAvailableClassroom_ReturnsNull_WhenOccupied()
    {
        var session = TestData.Session(requiredClassroomType: ClassroomType.Gym);
        _state.Assign(session, day: 1, slot: 0, startMinute: 0, endMinute: 60, classroomId: TestData.GymClassroomId);

        var id = _state.FindAvailableClassroom(day: 1, startMinute: 0, endMinute: 60, requiredType: ClassroomType.Gym,
            TestData.DefaultSchool().Classrooms);

        id.Should().BeNull();
    }

    [Fact]
    public void Assign_AddsToAssignedList()
    {
        var session = TestData.Session();
        _state.Assign(session, day: 1, slot: 0, startMinute: 0, endMinute: 60, classroomId: TestData.RegularClassroomId);

        _state.Assigned.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new AssignedSlot(
                session.AssignmentId, session.GroupId, session.TeacherId,
                session.AllocationId, TestData.RegularClassroomId, 1, 0, 0, 60));
    }

    [Fact]
    public void Unassign_RemovesFromAssignedList()
    {
        var session = TestData.Session();
        _state.Assign(session, day: 1, slot: 0, startMinute: 0, endMinute: 60, classroomId: TestData.RegularClassroomId);
        _state.Unassign(session, day: 1, slot: 0, startMinute: 0, endMinute: 60, classroomId: TestData.RegularClassroomId);

        _state.Assigned.Should().BeEmpty();
        _state.IsTeacherBusy(session.TeacherId, day: 1, startMinute: 0, endMinute: 60).Should().BeFalse();
    }
}
