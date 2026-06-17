using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Groups;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Groups;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class GroupsHandlersTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid OtherSchoolId = Guid.NewGuid();
    private static readonly Guid StageId = Guid.NewGuid();
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SchoolStage CreateStage() => new()
    {
        Id = StageId,
        SchoolId = SchoolId,
        StageType = StageTypes.Primaria,
        Name = "Educación Primaria",
    };

    private static CourseGroup CreateGroup() => new()
    {
        Id = GroupId,
        SchoolId = SchoolId,
        StageId = StageId,
        CourseLevel = 1,
        GroupLabel = "A",
        StudentCount = 25,
    };

    private static Teacher CreateTeacher() => new()
    {
        Id = TeacherId,
        SchoolId = SchoolId,
        FullName = "Tutor Test",
        Email = "tutor@test.es",
    };

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

    private sealed class FakeGroupRepository(AppDbContext db) : IGroupRepository
    {
        public Task<CourseGroup?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.CourseGroups.Include(g => g.SubjectHoursList).FirstOrDefaultAsync(g => g.Id == id, ct);

        public Task<IReadOnlyList<CourseGroup>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CourseGroup>>(
                db.CourseGroups.Where(g => g.SchoolId == schoolId).ToList());

        public Task AddAsync(CourseGroup group, CancellationToken ct)
        {
            db.CourseGroups.Add(group);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CourseGroup group, CancellationToken ct)
        {
            db.CourseGroups.Remove(group);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    private sealed class FakeCycleResolver : ICycleResolver
    {
        public int ResolveCycle(string stageType, int courseLevel) => 1;
    }

    [Fact]
    public async Task GetAll_ReturnsGroupsForSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(CreateStage());
        db.CourseGroups.Add(CreateGroup());
        await db.SaveChangesAsync();

        var handler = new GetAllGroupsHandler(db, new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(new GetAllGroupsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].GroupLabel.Should().Be("A");
        result[0].DisplayName.Should().Be("1ºA");
    }

    [Fact]
    public async Task GetAll_FiltersByStageId()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        var stage1 = CreateStage();
        var stage2 = new SchoolStage { Id = Guid.NewGuid(), SchoolId = SchoolId, StageType = StageTypes.Infantil, Name = "Infantil" };
        db.SchoolStages.AddRange(stage1, stage2);
        db.CourseGroups.Add(CreateGroup());
        db.CourseGroups.Add(new CourseGroup
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            StageId = stage2.Id,
            CourseLevel = 1,
            GroupLabel = "B",
            StudentCount = 20,
        });
        await db.SaveChangesAsync();

        var handler = new GetAllGroupsHandler(db, new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(new GetAllGroupsQuery(stage2.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].GroupLabel.Should().Be("B");
    }

    [Fact]
    public async Task GetAll_ExcludesOtherSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(CreateStage());
        db.CourseGroups.Add(CreateGroup());
        await db.SaveChangesAsync();

        var handler = new GetAllGroupsHandler(db, new FakeCurrentUser(OtherSchoolId), new FakeCycleResolver());
        var result = await handler.Handle(new GetAllGroupsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_CreatesGroup()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(CreateStage());
        await db.SaveChangesAsync();

        var handler = new CreateGroupHandler(db, new FakeGroupRepository(db), new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(
            new CreateGroupCommand(StageId, 2, "B", 20, null, null, null), CancellationToken.None);

        result.CourseLevel.Should().Be(2);
        result.GroupLabel.Should().Be("B");
        result.DisplayName.Should().Be("2ºB");

        var saved = await db.CourseGroups.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.SchoolId.Should().Be(SchoolId);
    }

    [Fact]
    public async Task Create_CreatesGroup_WithSubjectHours()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(CreateStage());
        await db.SaveChangesAsync();

        var handler = new CreateGroupHandler(db, new FakeGroupRepository(db), new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(
            new CreateGroupCommand(StageId, 1, "A", 25, null, null, new Dictionary<string, int> { ["mat"] = 5, ["len"] = 4 }),
            CancellationToken.None);

        result.SubjectHours.Should().HaveCount(2);
        result.SubjectHours["mat"].Should().Be(5);
        result.SubjectHours["len"].Should().Be(4);
    }

    [Fact]
    public async Task Update_UpdatesFields()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(CreateStage());
        db.CourseGroups.Add(CreateGroup());
        await db.SaveChangesAsync();

        var handler = new UpdateGroupHandler(db, new FakeGroupRepository(db), new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(
            new UpdateGroupCommand(GroupId, CourseLevel: 3, GroupLabel: "C", StudentCount: 30, null, null, null),
            CancellationToken.None);

        result.CourseLevel.Should().Be(3);
        result.GroupLabel.Should().Be("C");
        result.DisplayName.Should().Be("3ºC");
    }

    [Fact]
    public async Task Update_KeepsSubjectHours_WhenNotProvided()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(CreateStage());
        var group = CreateGroup();
        db.CourseGroups.Add(group);
        db.GroupSubjectHours.Add(new GroupSubjectHour { Id = Guid.NewGuid(), GroupId = GroupId, SubjectKey = "mat", Hours = 3 });
        await db.SaveChangesAsync();

        var handler = new UpdateGroupHandler(db, new FakeGroupRepository(db), new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(
            new UpdateGroupCommand(GroupId, CourseLevel: null, GroupLabel: "B", StudentCount: 30, TutorId: null, HomeClassroomId: null, SubjectHours: null),
            CancellationToken.None);

        result.GroupLabel.Should().Be("B");
        result.StudentCount.Should().Be(30);
    }

    [Fact]
    public async Task Update_WrongSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = OtherSchoolId, Name = "Other", Slug = "other" });
        db.CourseGroups.Add(CreateGroup());
        await db.SaveChangesAsync();

        var handler = new UpdateGroupHandler(db, new FakeGroupRepository(db), new FakeCurrentUser(OtherSchoolId), new FakeCycleResolver());
        var act = async () => await handler.Handle(
            new UpdateGroupCommand(GroupId, CourseLevel: 2, null, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_DeletesGroup()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.CourseGroups.Add(CreateGroup());
        await db.SaveChangesAsync();

        var handler = new DeleteGroupHandler(new FakeGroupRepository(db), new FakeCurrentUser(SchoolId));
        await handler.Handle(new DeleteGroupCommand(GroupId), CancellationToken.None);

        var group = await db.CourseGroups.FindAsync(GroupId);
        group.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WrongSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        db.CourseGroups.Add(CreateGroup());
        await db.SaveChangesAsync();

        var handler = new DeleteGroupHandler(new FakeGroupRepository(db), new FakeCurrentUser(OtherSchoolId));
        var act = async () => await handler.Handle(new DeleteGroupCommand(GroupId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
