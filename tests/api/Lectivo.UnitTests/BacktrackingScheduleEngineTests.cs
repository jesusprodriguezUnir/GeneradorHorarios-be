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
    public async Task GenerateAsync_WithBreakSlot_DoesNotScheduleOnBreak()
    {
        var slots = new List<SlotConfig>
        {
            new(0, IsBreak: false),
            new(1, IsBreak: false),
            new(2, IsBreak: true),
            new(3, IsBreak: false),
            new(4, IsBreak: false),
            new(5, IsBreak: false),
        };

        var school = new SchoolConfig(6, 5, slots, new List<ClassroomInfo>
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
