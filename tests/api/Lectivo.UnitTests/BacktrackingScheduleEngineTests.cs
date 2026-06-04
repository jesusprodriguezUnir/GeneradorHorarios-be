using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class BacktrackingScheduleEngineTests
{
    private readonly BacktrackingScheduleEngine _engine = new();

    [Fact]
    public async Task GenerateAsync_TrivialScenario_ReturnsComplete()
    {
        var sessions = new List<SessionToAssign>
        {
            TestData.Session(subjectName: "Lengua"),
            TestData.Session(assignmentId: Guid.NewGuid(), subjectName: "Mates"),
        };

        var context = TestData.Context(sessions: sessions);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        result.Status.Should().Be(GenerationStatus.Complete);
        result.TotalAssigned.Should().Be(sessions.Count);
        result.TotalRequired.Should().Be(sessions.Count);
        result.AssignedSlots.Should().HaveCount(sessions.Count);
    }

    [Fact]
    public async Task GenerateAsync_NoTeacherOverlapInResult()
    {
        var teacher = TestData.Teacher1Id;
        var sessions = new List<SessionToAssign>
        {
            TestData.Session(assignmentId: Guid.NewGuid(), teacherId: teacher, subjectName: "A"),
            TestData.Session(assignmentId: Guid.NewGuid(), teacherId: teacher, subjectName: "B"),
            TestData.Session(assignmentId: Guid.NewGuid(), teacherId: teacher, subjectName: "C"),
        };

        var context = TestData.Context(sessions: sessions);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        var teacherSlots = result.AssignedSlots
            .Where(a => a.TeacherId == teacher)
            .Select(a => (a.DayOfWeek, a.SlotIndex))
            .ToList();

        teacherSlots.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GenerateAsync_NoClassroomOverlapInResult()
    {
        var sessions = new List<SessionToAssign>
        {
            TestData.Session(assignmentId: Guid.NewGuid()),
            TestData.Session(assignmentId: Guid.NewGuid()),
            TestData.Session(assignmentId: Guid.NewGuid()),
        };

        var context = TestData.Context(sessions: sessions);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        var classroomSlots = result.AssignedSlots
            .Select(a => (a.ClassroomId, a.DayOfWeek, a.SlotIndex))
            .ToList();

        classroomSlots.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GenerateAsync_RequiredClassroomType_UsesMatchingClassroom()
    {
        var sessions = new List<SessionToAssign>
        {
            TestData.Session(
                assignmentId: Guid.NewGuid(),
                requiredClassroomType: ClassroomType.Gym),
        };

        var context = TestData.Context(sessions: sessions);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        result.Status.Should().Be(GenerationStatus.Complete);
        result.AssignedSlots.Should().ContainSingle()
            .Which.ClassroomId.Should().Be(TestData.GymClassroomId);
    }

    [Fact]
    public async Task GenerateAsync_Overconstrained_ReturnsPartialOrFailed()
    {
        // Más sesiones que slots disponibles (1 grupo, 1 teacher, 5 slots/día × 5 días = 25 slots,
        // pero solo 1 aula regular para las sesiones normales; forzamos 50 sesiones del mismo teacher)
        var sessions = Enumerable.Range(0, 30)
            .Select(i => TestData.Session(
                assignmentId: Guid.NewGuid(),
                teacherId: TestData.Teacher1Id,
                subjectName: $"S{i}"))
            .ToList();

        var context = TestData.Context(sessions: sessions, school: TestData.DefaultSchool(5));

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        result.Status.Should().BeOneOf(GenerationStatus.Partial, GenerationStatus.Failed);
        result.Conflicts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_VeryLowTimeout_DoesNotHang()
    {
        var sessions = Enumerable.Range(0, 10)
            .Select(i => TestData.Session(assignmentId: Guid.NewGuid(), subjectName: $"S{i}"))
            .ToList();

        var context = TestData.Context(
            sessions: sessions,
            timeoutSeconds: 1);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _engine.GenerateAsync(context, CancellationToken.None);
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_AlreadyCancelled_ThrowsTaskCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sessions = new List<SessionToAssign> { TestData.Session() };
        var context = TestData.Context(sessions: sessions);

        await Assert.ThrowsAsync<TaskCanceledException>(() => _engine.GenerateAsync(context, cts.Token));
    }

    [Fact]
    public async Task GenerateAsync_ReportsProgress()
    {
        var progressReports = new List<GenerationProgress>();
        var progress = new Progress<GenerationProgress>(p => progressReports.Add(p));

        var sessions = Enumerable.Range(0, 5)
            .Select(i => TestData.Session(assignmentId: Guid.NewGuid(), subjectName: $"S{i}"))
            .ToList();

        var context = TestData.Context(sessions: sessions);

        await _engine.GenerateAsync(context, CancellationToken.None, progress);

        progressReports.Should().NotBeEmpty();
        progressReports.Last().Percentage.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_MultiCycle_DisjointSchedules_SameTeacherAllowed()
    {
        var teacher = TestData.Teacher1Id;
        var group1 = TestData.Group1Id;
        var group2 = Guid.Parse("00000000-0000-0000-0003-000000000002");

        var cycle1Slots = new List<SlotConfig>
        {
            new(0, false, 540, 600),  // 09:00-10:00
            new(1, false, 600, 660),  // 10:00-11:00
            new(2, false, 660, 720),  // 11:00-12:00
        };
        var cycle2Slots = new List<SlotConfig>
        {
            new(0, false, 900, 960),  // 15:00-16:00 (disjoint)
            new(1, false, 960, 1020), // 16:00-17:00
            new(2, false, 1020, 1080),// 17:00-18:00
        };

        var cycles = new List<CycleGrid>
        {
            new(1, cycle1Slots),
            new(2, cycle2Slots),
        };

        var school = new SchoolConfig(3, 5, [1, 2, 3, 4, 5], cycles,
        [
            new(TestData.RegularClassroomId, "Aula 1", ClassroomType.Regular),
            new(Guid.NewGuid(), "Aula 2", ClassroomType.Regular),
        ]);

        var sessions = new List<SessionToAssign>
        {
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group1, teacherId: teacher, subjectName: "A", cycle: 1),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group2, teacherId: teacher, subjectName: "B", cycle: 2),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group1, teacherId: teacher, subjectName: "C", cycle: 1),
        };

        var context = TestData.Context(sessions: sessions, school: school);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        result.Status.Should().Be(GenerationStatus.Complete);
        result.TotalAssigned.Should().Be(3);

        var teacherSlots = result.AssignedSlots
            .Where(a => a.TeacherId == teacher)
            .ToList();

        var overlap = teacherSlots
            .GroupBy(a => a.DayOfWeek)
            .Any(g =>
            {
                var times = g.Select(a => (a.StartMinute, a.EndMinute)).OrderBy(t => t.StartMinute).ToList();
                for (int i = 1; i < times.Count; i++)
                {
                    if (times[i].StartMinute < times[i - 1].EndMinute)
                        return true;
                }
                return false;
            });

        overlap.Should().BeFalse("teacher should not have overlapping time intervals across cycles");
    }

    [Fact]
    public async Task GenerateAsync_MultiCycle_OverlappingSlots_TeacherConflictDetected()
    {
        var teacher = TestData.Teacher1Id;
        var group1 = TestData.Group1Id;
        var group2 = Guid.Parse("00000000-0000-0000-0003-000000000002");

        var cycleSlots = new List<SlotConfig>
        {
            new(0, false, 540, 600),  // 09:00-10:00
            new(1, false, 600, 660),  // 10:00-11:00
            new(2, false, 660, 720),  // 11:00-12:00
        };

        var cycles = new List<CycleGrid>
        {
            new(1, cycleSlots),
            new(2, cycleSlots), // same timing → all slots overlap
        };

        // A single regular classroom can only hold one session at a time
        var school = new SchoolConfig(3, 2, [1, 2], cycles,
        [
            new(TestData.RegularClassroomId, "Aula 1", ClassroomType.Regular),
        ]);

        // More sessions than available (teacher can only teach 1 session per time slot,
        // and there's only 1 classroom for 6 sessions over 2 days × 3 slots = 6 available)
        var sessions = new List<SessionToAssign>
        {
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group1, teacherId: teacher, subjectName: "A", cycle: 1),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group1, teacherId: teacher, subjectName: "B", cycle: 1),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group1, teacherId: teacher, subjectName: "C", cycle: 1),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group2, teacherId: teacher, subjectName: "D", cycle: 2),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group2, teacherId: teacher, subjectName: "E", cycle: 2),
            TestData.Session(assignmentId: Guid.NewGuid(), groupId: group2, teacherId: teacher, subjectName: "F", cycle: 2),
        };

        var context = TestData.Context(sessions: sessions, school: school);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        result.TotalAssigned.Should().Be(6);

        var overlaps = result.AssignedSlots
            .Where(a => a.TeacherId == teacher)
            .GroupBy(a => a.DayOfWeek)
            .Any(g =>
            {
                var times = g.Select(a => (a.StartMinute, a.EndMinute)).OrderBy(t => t.StartMinute).ToList();
                for (int i = 1; i < times.Count; i++)
                {
                    if (times[i].StartMinute < times[i - 1].EndMinute)
                        return true;
                }
                return false;
            });

        overlaps.Should().BeFalse("no overlapping teacher assignments should exist across cycles");
    }

    [Fact]
    public async Task GenerateAsync_WithBreakSlot_DoesNotScheduleOnBreak()
    {
        var slots = new List<SlotConfig>
        {
            new(0, IsBreak: false, StartMinute: 0, EndMinute: 60),
            new(1, IsBreak: false, StartMinute: 60, EndMinute: 120),
            new(2, IsBreak: true, StartMinute: 120, EndMinute: 150),
            new(3, IsBreak: false, StartMinute: 150, EndMinute: 210),
            new(4, IsBreak: false, StartMinute: 210, EndMinute: 270),
            new(5, IsBreak: false, StartMinute: 270, EndMinute: 330),
        };

        var cycles = new List<CycleGrid> { new(1, slots) };

        var school = new SchoolConfig(6, 5, [1, 2, 3, 4, 5], cycles, new List<ClassroomInfo>
        {
            new(TestData.RegularClassroomId, "Aula 1", ClassroomType.Regular)
        });

        var sessions = Enumerable.Range(0, 5)
            .Select(i => TestData.Session(assignmentId: Guid.NewGuid(), subjectName: $"S{i}"))
            .ToList();

        var context = TestData.Context(sessions: sessions, school: school);

        var result = await _engine.GenerateAsync(context, CancellationToken.None);

        result.Status.Should().Be(GenerationStatus.Complete);
        result.TotalAssigned.Should().Be(5);
        result.AssignedSlots.Should().NotContain(a => a.SlotIndex == 2);
        result.AssignedSlots.Select(a => a.SlotIndex).Should().OnlyContain(s => s == 0 || s == 1 || s == 3 || s == 4 || s == 5);
    }
}
