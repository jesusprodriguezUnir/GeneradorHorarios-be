using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Features.Auth;
using HorariosEscolares.Application.Features.Schools;
using HorariosEscolares.Application.Features.Schedules;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class RoleBasedFilteringTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid TeacherUserId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid Stage1Id = Guid.NewGuid();
    private static readonly Guid Stage2Id = Guid.NewGuid();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedDatabaseAsync(AppDbContext db)
    {
        // 1. School
        db.Schools.Add(new School { Id = SchoolId, Name = "Test School", Slug = "test" });

        // 2. Stages
        var stage1 = new SchoolStage { Id = Stage1Id, SchoolId = SchoolId, StageType = "primaria", Name = "Primaria" };
        var stage2 = new SchoolStage { Id = Stage2Id, SchoolId = SchoolId, StageType = "infantil", Name = "Infantil" };
        db.SchoolStages.AddRange(stage1, stage2);

        // 3. User & Teacher
        db.AppUsers.Add(new AppUser
        {
            Id = TeacherUserId,
            Email = "teacher@school.com",
            FullName = "Teacher One",
            SchoolId = SchoolId,
            Role = "teacher",
            TeacherId = TeacherId
        });

        db.Teachers.Add(new Teacher
        {
            Id = TeacherId,
            UserId = TeacherUserId,
            SchoolId = SchoolId,
            FullName = "Teacher One",
            Email = "teacher@school.com"
        });

        // 4. Associate Teacher only to Stage 1 (Primaria)
        db.TeacherStageAssignments.Add(new TeacherStageAssignment
        {
            TeacherId = TeacherId,
            StageId = Stage1Id,
            Cycle = null
        });

        // 5. School Periods
        var period1 = new SchoolPeriod { Id = Guid.NewGuid(), SchoolId = SchoolId, StageId = Stage1Id, Key = "ord1", Name = "Period Stage 1", IsDefault = true };
        var period2 = new SchoolPeriod { Id = Guid.NewGuid(), SchoolId = SchoolId, StageId = Stage2Id, Key = "ord2", Name = "Period Stage 2", IsDefault = true };
        db.SchoolPeriods.AddRange(period1, period2);

        // 6. Schedules
        var schedule1 = new ScheduleRecord { Id = Guid.NewGuid(), SchoolId = SchoolId, StageId = Stage1Id, Status = "published", AcademicYear = "2025/2026", CreatedBy = Guid.Empty };
        var schedule2 = new ScheduleRecord { Id = Guid.NewGuid(), SchoolId = SchoolId, StageId = Stage2Id, Status = "published", AcademicYear = "2025/2026", CreatedBy = Guid.Empty };
        db.Schedules.AddRange(schedule1, schedule2);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCurrentUser_ForTeacher_ReturnsAssignedStageTypes()
    {
        await using var db = CreateDb();
        await SeedDatabaseAsync(db);

        var currentUser = new FakeCurrentUser(SchoolId, TeacherUserId, "teacher", isAdmin: false, isTeacher: true);
        var handler = new GetCurrentUserHandler(db, currentUser);

        var response = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        response.Should().NotBeNull();
        response!.Teacher.Should().NotBeNull();
        response.Teacher!.AssignedStageTypes.Should().ContainSingle().Which.Should().Be("primaria");
    }

    [Fact]
    public async Task GetStages_ForTeacher_ReturnsOnlyAssignedStages()
    {
        await using var db = CreateDb();
        await SeedDatabaseAsync(db);

        // Act as Teacher (assigned to Stage1/primaria only)
        var currentUser = new FakeCurrentUser(SchoolId, TeacherUserId, "teacher", isAdmin: false, isTeacher: true);
        var handler = new GetStagesHandler(db, currentUser);

        var result = await handler.Handle(new GetStagesQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(Stage1Id);
        result[0].StageType.Should().Be("primaria");
    }

    [Fact]
    public async Task GetStages_ForAdmin_ReturnsAllStages()
    {
        await using var db = CreateDb();
        await SeedDatabaseAsync(db);

        // Act as Admin (Director)
        var currentUser = new FakeCurrentUser(SchoolId, Guid.NewGuid(), "school_admin", isAdmin: true, isTeacher: false);
        var handler = new GetStagesHandler(db, currentUser);

        var result = await handler.Handle(new GetStagesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPeriods_ForTeacher_ReturnsOnlyPeriodsOfAssignedStages()
    {
        await using var db = CreateDb();
        await SeedDatabaseAsync(db);

        var currentUser = new FakeCurrentUser(SchoolId, TeacherUserId, "teacher", isAdmin: false, isTeacher: true);
        var handler = new GetPeriodsHandler(db, currentUser);

        var result = await handler.Handle(new GetPeriodsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].StageId.Should().Be(Stage1Id);
        result[0].Name.Should().Be("Period Stage 1");
    }

    [Fact]
    public async Task GetSchedulesList_ForTeacher_ReturnsOnlySchedulesOfAssignedStages()
    {
        await using var db = CreateDb();
        await SeedDatabaseAsync(db);

        var currentUser = new FakeCurrentUser(SchoolId, TeacherUserId, "teacher", isAdmin: false, isTeacher: true);
        var handler = new GetSchedulesListHandler(db, currentUser);

        var result = await handler.Handle(new GetSchedulesListQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].StageId.Should().Be(Stage1Id);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(Guid schoolId, Guid userId, string role, bool isAdmin, bool isTeacher)
        {
            SchoolId = schoolId;
            UserId = userId;
            Role = role;
            IsAdmin = isAdmin;
            IsTeacher = isTeacher;
        }

        public Guid UserId { get; }
        public Guid SchoolId { get; }
        public string Role { get; }
        public bool IsAdmin { get; }
        public bool IsTeacher { get; }
    }
}
