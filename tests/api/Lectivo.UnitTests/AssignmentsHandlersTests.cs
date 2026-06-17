using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Assignments;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Assignments;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class AssignmentsHandlersTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid OtherSchoolId = Guid.NewGuid();
    private static readonly Guid StageId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid AllocationId = Guid.NewGuid();
    private static readonly Guid AssignmentId = Guid.NewGuid();

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedBaseAsync(AppDbContext db)
    {
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.SchoolStages.Add(new SchoolStage { Id = StageId, SchoolId = SchoolId, StageType = StageTypes.Primaria, Name = "Primaria" });
        db.Teachers.Add(new Teacher { Id = TeacherId, SchoolId = SchoolId, FullName = "Profe Test", Email = "profe@test.es" });
        db.CourseGroups.Add(new CourseGroup { Id = GroupId, SchoolId = SchoolId, StageId = StageId, CourseLevel = 1, GroupLabel = "A", StudentCount = 25 });
        db.SubjectAllocations.Add(new SubjectAllocation { Id = AllocationId, TemplateId = Guid.NewGuid(), SubjectKey = "mat", SubjectName = "Matematicas", WeeklyHoursDefault = 5 });
        await db.SaveChangesAsync();
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

    private sealed class FakeAssignmentRepository(AppDbContext db) : IAssignmentRepository
    {
        public Task<Assignment?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.Assignments.FirstOrDefaultAsync(a => a.Id == id, ct);

        public Task<IReadOnlyList<Assignment>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Assignment>>(
                db.Assignments.Where(a => a.SchoolId == schoolId).ToList());

        public Task<IReadOnlyList<Assignment>> GetByTeacherAsync(Guid teacherId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Assignment>>(
                db.Assignments.Where(a => a.TeacherId == teacherId).ToList());

        public Task AddAsync(Assignment assignment, CancellationToken ct)
        {
            db.Assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Assignment assignment, CancellationToken ct)
        {
            db.Assignments.Remove(assignment);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    private sealed class FakeCycleResolver : ICycleResolver
    {
        public int ResolveCycle(string stageType, int courseLevel) => 1;
    }

    [Fact]
    public async Task GetAll_ReturnsAssignmentSummary()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Assignments.Add(new Assignment
        {
            Id = AssignmentId,
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 4,
        });
        await db.SaveChangesAsync();

        var handler = new GetAllAssignmentsHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetAllAssignmentsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].SubjectName.Should().Be("Matematicas");
        result[0].AssignedHours.Should().Be(4);
        result[0].Assignments.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAll_ExcludesOtherSchool()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Assignments.Add(new Assignment
        {
            Id = AssignmentId,
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 4,
        });
        await db.SaveChangesAsync();

        var handler = new GetAllAssignmentsHandler(db, new FakeCurrentUser(OtherSchoolId));
        var result = await handler.Handle(new GetAllAssignmentsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_CreatesAssignment()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var handler = new CreateAssignmentHandler(db, new FakeAssignmentRepository(db), new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var result = await handler.Handle(
            new CreateAssignmentCommand(TeacherId, GroupId, AllocationId, 4), CancellationToken.None);

        result.TeacherName.Should().Be("Profe Test");
        result.WeeklyHours.Should().Be(4);

        var saved = await db.Assignments.FirstOrDefaultAsync(a => a.TeacherId == TeacherId);
        saved.Should().NotBeNull();
        saved!.SchoolId.Should().Be(SchoolId);
    }

    [Fact]
    public async Task Create_InvalidTeacher_Throws()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var handler = new CreateAssignmentHandler(db, new FakeAssignmentRepository(db), new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var act = async () => await handler.Handle(
            new CreateAssignmentCommand(Guid.NewGuid(), GroupId, AllocationId, 4), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no válido*");
    }

    [Fact]
    public async Task Update_BulkUpdate_ReplacesAssignments()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        var existing = new Assignment
        {
            Id = AssignmentId,
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 3,
        };
        db.Assignments.Add(existing);
        await db.SaveChangesAsync();

        var handler = new UpdateAssignmentsHandler(db, new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        await handler.Handle(
            new UpdateAssignmentsCommand([new AssignmentUpdateInput(TeacherId, GroupId, AllocationId, 5)]),
            CancellationToken.None);

        var updated = await db.Assignments.FirstAsync(a => a.Id == AssignmentId);
        updated.WeeklyHours.Should().Be(5);
    }

    [Fact]
    public async Task Update_BulkUpdate_InvalidTeacher_Throws()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var handler = new UpdateAssignmentsHandler(db, new FakeCurrentUser(SchoolId), new FakeCycleResolver());
        var act = async () => await handler.Handle(
            new UpdateAssignmentsCommand([new AssignmentUpdateInput(Guid.NewGuid(), GroupId, AllocationId, 5)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no válido*");
    }

    [Fact]
    public async Task Delete_DeletesAssignment()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Assignments.Add(new Assignment
        {
            Id = AssignmentId,
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 3,
        });
        await db.SaveChangesAsync();

        var handler = new DeleteAssignmentHandler(new FakeAssignmentRepository(db), new FakeCurrentUser(SchoolId));
        await handler.Handle(new DeleteAssignmentCommand(AssignmentId), CancellationToken.None);

        var assignment = await db.Assignments.FindAsync(AssignmentId);
        assignment.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WrongSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Assignments.Add(new Assignment
        {
            Id = AssignmentId,
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 3,
        });
        await db.SaveChangesAsync();

        var handler = new DeleteAssignmentHandler(new FakeAssignmentRepository(db), new FakeCurrentUser(OtherSchoolId));
        var act = async () => await handler.Handle(new DeleteAssignmentCommand(AssignmentId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
