using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Classrooms;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Classrooms;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class ClassroomsHandlersTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid OtherSchoolId = Guid.NewGuid();
    private static readonly Guid ClassroomId = Guid.NewGuid();

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Classroom CreateClassroom() => new()
    {
        Id = ClassroomId,
        SchoolId = SchoolId,
        Name = "Aula 1A",
        ClassroomType = "Classroom",
        Capacity = 25,
        IsShared = false,
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

    private sealed class FakeClassroomRepository(AppDbContext db) : IClassroomRepository
    {
        public Task<Classroom?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.Classrooms.FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<IReadOnlyList<Classroom>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Classroom>>(
                db.Classrooms.Where(c => c.SchoolId == schoolId).ToList());

        public Task AddAsync(Classroom classroom, CancellationToken ct)
        {
            db.Classrooms.Add(classroom);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Classroom classroom, CancellationToken ct)
        {
            db.Classrooms.Remove(classroom);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task GetAll_ReturnsClassroomsForSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Classrooms.Add(CreateClassroom());
        await db.SaveChangesAsync();

        var handler = new GetAllClassroomsHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetAllClassroomsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Aula 1A");
    }

    [Fact]
    public async Task GetAll_ExcludesOtherSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Classrooms.Add(CreateClassroom());
        await db.SaveChangesAsync();

        var handler = new GetAllClassroomsHandler(db, new FakeCurrentUser(OtherSchoolId));
        var result = await handler.Handle(new GetAllClassroomsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_CreatesClassroom()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        await db.SaveChangesAsync();

        var handler = new CreateClassroomHandler(new FakeClassroomRepository(db), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(
            new CreateClassroomCommand("Aula Nueva", "Classroom", 30, false, null), CancellationToken.None);

        result.Name.Should().Be("Aula Nueva");
        result.Capacity.Should().Be(30);

        var saved = await db.Classrooms.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.SchoolId.Should().Be(SchoolId);
    }

    [Fact]
    public async Task Update_UpdatesFields()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Classrooms.Add(CreateClassroom());
        await db.SaveChangesAsync();

        var handler = new UpdateClassroomHandler(new FakeClassroomRepository(db), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(
            new UpdateClassroomCommand(ClassroomId, Name: "Aula Renovada", ClassroomType: null, Capacity: 30, IsShared: true, StageId: null),
            CancellationToken.None);

        result.Name.Should().Be("Aula Renovada");
        result.Capacity.Should().Be(30);
        result.IsShared.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WrongSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = OtherSchoolId, Name = "Other", Slug = "other" });
        db.Classrooms.Add(CreateClassroom());
        await db.SaveChangesAsync();

        var handler = new UpdateClassroomHandler(new FakeClassroomRepository(db), new FakeCurrentUser(OtherSchoolId));
        var act = async () => await handler.Handle(
            new UpdateClassroomCommand(ClassroomId, Name: "Hack", null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_DeletesClassroom()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Classrooms.Add(CreateClassroom());
        await db.SaveChangesAsync();

        var handler = new DeleteClassroomHandler(new FakeClassroomRepository(db), new FakeCurrentUser(SchoolId));
        await handler.Handle(new DeleteClassroomCommand(ClassroomId), CancellationToken.None);

        var classroom = await db.Classrooms.FindAsync(ClassroomId);
        classroom.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WrongSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        db.Classrooms.Add(CreateClassroom());
        await db.SaveChangesAsync();

        var handler = new DeleteClassroomHandler(new FakeClassroomRepository(db), new FakeCurrentUser(OtherSchoolId));
        var act = async () => await handler.Handle(new DeleteClassroomCommand(ClassroomId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
