using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Constraints;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class ConstraintsHandlersTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid OtherSchoolId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid ConstraintId = Guid.NewGuid();

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Teacher CreateTeacher() => new()
    {
        Id = TeacherId,
        SchoolId = SchoolId,
        FullName = "Profe Constraint",
        Email = "constraint@test.es",
    };

    private static TeacherConstraint CreateConstraint() => new()
    {
        Id = ConstraintId,
        SchoolId = SchoolId,
        TeacherId = TeacherId,
        ConstraintType = "Preferred",
        DayOfWeek = 1,
        SlotIndex = 0,
        Weight = 10,
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

    private sealed class FakeConstraintRepository(AppDbContext db) : IConstraintRepository
    {
        public Task<TeacherConstraint?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.TeacherConstraints.FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<IReadOnlyList<TeacherConstraint>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TeacherConstraint>>(
                db.TeacherConstraints.Where(c => c.SchoolId == schoolId).ToList());

        public Task<IReadOnlyList<TeacherConstraint>> GetByTeacherAsync(Guid teacherId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TeacherConstraint>>(
                db.TeacherConstraints.Where(c => c.TeacherId == teacherId).ToList());

        public Task AddAsync(TeacherConstraint constraint, CancellationToken ct)
        {
            db.TeacherConstraints.Add(constraint);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TeacherConstraint constraint, CancellationToken ct)
        {
            db.TeacherConstraints.Remove(constraint);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task GetAll_ReturnsConstraintsForSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Teachers.Add(CreateTeacher());
        db.TeacherConstraints.Add(CreateConstraint());
        await db.SaveChangesAsync();

        var handler = new GetAllConstraintsHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetAllConstraintsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ConstraintType.Should().Be("Preferred");
        result[0].TeacherName.Should().Be("Profe Constraint");
    }

    [Fact]
    public async Task GetAll_ExcludesOtherSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = OtherSchoolId, Name = "Other", Slug = "other" });
        db.Teachers.Add(CreateTeacher());
        db.TeacherConstraints.Add(CreateConstraint());
        await db.SaveChangesAsync();

        var handler = new GetAllConstraintsHandler(db, new FakeCurrentUser(OtherSchoolId));
        var result = await handler.Handle(new GetAllConstraintsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByTeacher_ReturnsFilteredConstraints()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Teachers.Add(CreateTeacher());
        db.TeacherConstraints.Add(CreateConstraint());
        var otherTeacherId = Guid.NewGuid();
        db.Teachers.Add(new Teacher { Id = otherTeacherId, SchoolId = SchoolId, FullName = "Other", Email = "other@test.es" });
        db.TeacherConstraints.Add(new TeacherConstraint
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            TeacherId = otherTeacherId,
            ConstraintType = "Unavailable",
            DayOfWeek = 2,
            SlotIndex = 1,
            Weight = 5,
        });
        await db.SaveChangesAsync();

        var handler = new GetConstraintsByTeacherHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetConstraintsByTeacherQuery(TeacherId), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ConstraintType.Should().Be("Preferred");
    }

    [Fact]
    public async Task GetByTeacher_OnlyOwnSchool()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Teachers.Add(CreateTeacher());
        db.TeacherConstraints.Add(CreateConstraint());
        await db.SaveChangesAsync();

        var handler = new GetConstraintsByTeacherHandler(db, new FakeCurrentUser(OtherSchoolId));
        var result = await handler.Handle(new GetConstraintsByTeacherQuery(TeacherId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_CreatesConstraint()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Teachers.Add(CreateTeacher());
        await db.SaveChangesAsync();

        var handler = new CreateConstraintHandler(db, new FakeConstraintRepository(db), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(
            new CreateConstraintCommand(TeacherId, "Unavailable", 2, 3, 5, "Por la tarde"), CancellationToken.None);

        result.ConstraintType.Should().Be("Unavailable");
        result.DayOfWeek.Should().Be(2);
        result.SlotIndex.Should().Be(3);
        result.Weight.Should().Be(5);
        result.Reason.Should().Be("Por la tarde");

        var saved = await db.TeacherConstraints.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.SchoolId.Should().Be(SchoolId);
    }

    [Fact]
    public async Task Delete_DeletesConstraint()
    {
        await using var db = CreateDb();
        db.Schools.Add(new School { Id = SchoolId, Name = "Test", Slug = "test" });
        db.Teachers.Add(CreateTeacher());
        db.TeacherConstraints.Add(CreateConstraint());
        await db.SaveChangesAsync();

        var handler = new DeleteConstraintHandler(new FakeConstraintRepository(db), new FakeCurrentUser(SchoolId));
        await handler.Handle(new DeleteConstraintCommand(ConstraintId), CancellationToken.None);

        var constraint = await db.TeacherConstraints.FindAsync(ConstraintId);
        constraint.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WrongSchool_ThrowsNotFound()
    {
        await using var db = CreateDb();
        db.TeacherConstraints.Add(CreateConstraint());
        await db.SaveChangesAsync();

        var handler = new DeleteConstraintHandler(new FakeConstraintRepository(db), new FakeCurrentUser(OtherSchoolId));
        var act = async () => await handler.Handle(new DeleteConstraintCommand(ConstraintId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
