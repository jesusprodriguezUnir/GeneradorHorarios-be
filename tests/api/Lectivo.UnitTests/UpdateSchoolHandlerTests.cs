using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Schools;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class UpdateSchoolHandlerTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Handle_UpdateSchoolFields_UpdatesCorrectly()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
        };
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        var repo = new FakeSchoolRepository(db) { CurrentSchool = school };
        var user = new FakeCurrentUser(SchoolId);
        var handler = new UpdateSchoolHandler(db, repo, user);

        var command = new UpdateSchoolCommand(
            Name: "CEIP Updated", CenterCode: null, Locality: null, Community: null,
            MinCourseLevel: 1, MaxCourseLevel: 6, AcademicYear: "2026/2027",
            ScheduleType: "continua", MorningStart: "9:00", SlotMinutes: 60,
            BreakAfterSlot: 2, BreakMinutes: 30, SlotsPerDay: 5, AfternoonSlots: 0,
            AfternoonStart: null, WorkingDays: [1, 2, 3, 4, 5]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("CEIP Updated");
        result.AcademicYear.Should().Be("2026/2027");
        result.SlotsPerDay.Should().Be(5);
    }

    private sealed class FakeSchoolRepository(AppDbContext db) : ISchoolRepository
    {
        public School? CurrentSchool { get; set; }
        public bool SaveChangesCalled { get; set; }

        public Task<School?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(CurrentSchool);

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            SaveChangesCalled = true;
            await db.SaveChangesAsync(ct);
        }
    }

    private sealed class FakeCurrentUser(Guid schoolId) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid SchoolId { get; } = schoolId;
        public Guid RoleId => RoleIds.Director;
        public string RoleCode => RoleCodes.Director;
        public string RoleName => "Director";
        public RoleKind RoleKind => RoleKind.Admin;
        public bool IsAdmin => true;
        public bool IsTeacher => false;
    }
}
