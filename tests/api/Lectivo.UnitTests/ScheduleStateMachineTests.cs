using HorariosEscolares.Domain.Entities;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class ScheduleStateMachineTests
{
    [Fact]
    public void Create_SetsDraftStatus()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");

        schedule.Status.Should().Be(ScheduleStatus.Draft);
    }

    [Fact]
    public void SetGenerated_FromDraft_SetsGeneratedStatus()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");

        schedule.SetGenerated([], []);

        schedule.Status.Should().Be(ScheduleStatus.Generated);
        schedule.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Publish_FromGenerated_SetsPublishedStatus()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");
        schedule.SetGenerated([], []);

        schedule.Publish();

        schedule.Status.Should().Be(ScheduleStatus.Published);
        schedule.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Archive_FromPublished_SetsArchivedStatus()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");
        schedule.SetGenerated([], []);
        schedule.Publish();

        schedule.Archive();

        schedule.Status.Should().Be(ScheduleStatus.Archived);
    }

    [Fact]
    public void Publish_FromDraft_ThrowsInvalidOperationException()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");

        Action act = () => schedule.Publish();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetGenerated_FromGenerated_ThrowsInvalidOperationException()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");
        schedule.SetGenerated([], []);

        Action act = () => schedule.SetGenerated([], []);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_FromDraft_ThrowsInvalidOperationException()
    {
        var schedule = Schedule.Create(Guid.NewGuid(), "2025-2026");

        Action act = () => schedule.Archive();

        act.Should().Throw<InvalidOperationException>();
    }
}
