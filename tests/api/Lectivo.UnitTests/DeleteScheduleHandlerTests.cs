using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Schedules;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Scheduling;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class DeleteScheduleHandlerTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid StageId = Guid.NewGuid();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ScheduleRecord AddSchedule(AppDbContext db, string status)
    {
        var s = new ScheduleRecord
        {
            SchoolId = SchoolId, StageId = StageId, AcademicYear = "2025/2026",
            Status = status, CreatedBy = Guid.Empty,
        };
        db.Schedules.Add(s);
        db.SaveChanges();
        return s;
    }

    [Fact]
    public async Task Delete_DraftSchedule_CallsRepository()
    {
        await using var db = CreateDb();
        var schedule = AddSchedule(db, "generated");
        var repo = new FakeScheduleRepository();
        var handler = new DeleteScheduleHandler(db, repo, new FakeCurrentUser());

        await handler.Handle(new DeleteScheduleCommand(schedule.Id), CancellationToken.None);

        repo.DeletedId.Should().Be(schedule.Id);
    }

    [Fact]
    public async Task Delete_PublishedSchedule_Throws()
    {
        await using var db = CreateDb();
        var schedule = AddSchedule(db, "published");
        var repo = new FakeScheduleRepository();
        var handler = new DeleteScheduleHandler(db, repo, new FakeCurrentUser());

        var act = async () => await handler.Handle(new DeleteScheduleCommand(schedule.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        repo.DeletedId.Should().BeNull();
    }

    [Fact]
    public async Task Delete_OtherSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var schedule = AddSchedule(db, "generated");
        var repo = new FakeScheduleRepository();
        // Usuario de otro colegio.
        var handler = new DeleteScheduleHandler(db, repo, new FakeCurrentUser(Guid.NewGuid()));

        var act = async () => await handler.Handle(new DeleteScheduleCommand(schedule.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repo.DeletedId.Should().BeNull();
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public Guid? DeletedId { get; private set; }

        public Task AddScheduleWithDetailsAsync(ScheduleRecord schedule,
            IReadOnlyList<ScheduleEntry> entries, IReadOnlyList<ScheduleConflictRecord> conflicts,
            CancellationToken ct) => Task.CompletedTask;

        public Task DeleteAsync(Guid scheduleId, CancellationToken ct)
        {
            DeletedId = scheduleId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(Guid? schoolId = null) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid SchoolId { get; } = schoolId ?? DeleteScheduleHandlerTests.SchoolId;
        public Guid RoleId => RoleIds.Director;
        public string RoleCode => RoleCodes.Director;
        public string RoleName => "Director";
        public RoleKind RoleKind => RoleKind.Admin;
        public bool IsAdmin => true;
        public bool IsTeacher => false;
    }
}
