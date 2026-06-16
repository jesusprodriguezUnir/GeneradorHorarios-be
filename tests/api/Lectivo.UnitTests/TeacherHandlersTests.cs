using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Features.Teachers;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Teachers;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class TeacherHandlersTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StageId = Guid.NewGuid();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static School CreateSchool() => new()
    {
        Id = SchoolId,
        Name = "CEIP Test",
        Slug = "ceip-test",
    };

    private static SchoolStage CreateStage() => new()
    {
        Id = StageId,
        SchoolId = SchoolId,
        StageType = "primaria",
        Name = "Educación Primaria",
    };

    private static Teacher CreateTeacher() => new()
    {
        Id = TeacherId,
        SchoolId = SchoolId,
        FullName = "Ana Test",
        Email = "ana@test.es",
        TeacherType = "tutor",
        MaxWeeklyHours = 25,
        ColorKey = "mat",
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

    private sealed class FakeTeacherRepository(AppDbContext db) : ITeacherRepository
    {
        public Task<Teacher?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);

        public Task<IReadOnlyList<Teacher>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Teacher>>(
                db.Teachers.Where(t => t.SchoolId == schoolId).ToList());

        public Task AddAsync(Teacher teacher, CancellationToken ct)
        {
            db.Teachers.Add(teacher);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Teacher teacher, CancellationToken ct)
        {
            db.Teachers.Remove(teacher);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task GetAllTeachersHandler_ReturnsTeachers()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        db.Teachers.Add(CreateTeacher());
        await db.SaveChangesAsync();

        var handler = new GetAllTeachersHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetAllTeachersQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].FullName.Should().Be("Ana Test");
    }

    [Fact]
    public async Task GetTeacherByIdHandler_ReturnsTeacher()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        db.Teachers.Add(CreateTeacher());
        await db.SaveChangesAsync();

        var handler = new GetTeacherByIdHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetTeacherByIdQuery(TeacherId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(TeacherId);
    }

    [Fact]
    public async Task CreateTeacherHandler_CreatesTeacher()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        await db.SaveChangesAsync();

        var handler = new CreateTeacherHandler(db, new FakeTeacherRepository(db), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new CreateTeacherCommand(
            "Luis Test", "luis@test.es", "tutor", 25,
            [new SubjectHourInput("mat", 5)], "len",
            [new StageAssignmentInput(StageId, null)]), CancellationToken.None);

        result.FullName.Should().Be("Luis Test");
        result.SubjectHours.Should().HaveCount(1);
        result.StageAssignments.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateTeacherHandler_UpdatesFields()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        db.Teachers.Add(CreateTeacher());
        await db.SaveChangesAsync();

        var handler = new UpdateTeacherHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new UpdateTeacherCommand(
            TeacherId, FullName: "Ana Actualizada", Email: null, TeacherType: null,
            MaxWeeklyHours: null, SubjectHours: null, ColorKey: null,
            StageAssignments: null), CancellationToken.None);

        result.FullName.Should().Be("Ana Actualizada");
    }

    [Fact]
    public async Task DeleteTeacherHandler_DeletesTeacher()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        db.Teachers.Add(CreateTeacher());
        await db.SaveChangesAsync();

        var handler = new DeleteTeacherHandler(new FakeTeacherRepository(db), new FakeCurrentUser(SchoolId));
        await handler.Handle(new DeleteTeacherCommand(TeacherId), CancellationToken.None);

        var teacher = await db.Teachers.FindAsync(TeacherId);
        teacher.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTeacherAssignmentsHandler_UpdatesAssignments()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        db.SchoolStages.Add(CreateStage());
        db.Teachers.Add(CreateTeacher());

        var groupId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        db.CourseGroups.Add(new CourseGroup
        {
            Id = groupId,
            SchoolId = SchoolId,
            StageId = StageId,
            CourseLevel = 1,
            GroupLabel = "A",
        });
        db.SubjectAllocations.Add(new SubjectAllocation
        {
            Id = allocationId,
            TemplateId = Guid.NewGuid(),
            SubjectKey = "mat",
            SubjectName = "Matemáticas",
        });
        await db.SaveChangesAsync();

        var handler = new UpdateTeacherAssignmentsHandler(db, new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(new UpdateTeacherAssignmentsCommand(
            TeacherId,
            [new TeacherAssignmentInput(allocationId, groupId, 3)]),
            CancellationToken.None);

        result.Assignments.Should().HaveCount(1);
        result.Assignments[0].WeeklyHours.Should().Be(3);
    }

    private sealed class FakeCycleResolver : ICycleResolver
    {
        public int ResolveCycle(string stageType, int courseLevel) => 1;
    }
}
