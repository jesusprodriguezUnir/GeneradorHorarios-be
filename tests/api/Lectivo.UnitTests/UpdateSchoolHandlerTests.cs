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
    public async Task Handle_AddStage_CreatesStagePeriodAndCycles()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
            Stage = "primaria"
        };
        db.Schools.Add(school);

        var primariaStage = new SchoolStage
        {
            SchoolId = SchoolId,
            StageType = StageTypes.Primaria,
            Name = "Educación Primaria"
        };
        db.SchoolStages.Add(primariaStage);
        await db.SaveChangesAsync();

        var repo = new FakeSchoolRepository(db) { CurrentSchool = school };
        var user = new FakeCurrentUser(SchoolId);
        var handler = new UpdateSchoolHandler(db, repo, user);

        var command = new UpdateSchoolCommand(
            Name: null, CenterCode: null, Locality: null, Community: null,
            Stage: "primaria,secundaria",
            MinCourseLevel: null, MaxCourseLevel: null, AcademicYear: null,
            ScheduleType: null, MorningStart: null, SlotMinutes: null,
            BreakAfterSlot: null, BreakMinutes: null, SlotsPerDay: null, AfternoonSlots: null,
            AfternoonStart: null, WorkingDays: null);

        var result = await handler.Handle(command, CancellationToken.None);

        // Verify school stage string updated
        school.Stage.Should().Be("primaria,secundaria");

        // Verify stage created
        var stages = await db.SchoolStages.Where(st => st.SchoolId == SchoolId).ToListAsync();
        stages.Should().HaveCount(2);
        stages.Should().Contain(st => st.StageType == StageTypes.Secundaria);
        
        var secStage = stages.First(st => st.StageType == StageTypes.Secundaria);
        secStage.Name.Should().Be("Educación Secundaria (ESO)");
        secStage.MinLevel.Should().Be(1);
        secStage.MaxLevel.Should().Be(4);
        secStage.SlotsPerDay.Should().Be(6);

        // Verify period created
        var periods = await db.SchoolPeriods.Where(p => p.StageId == secStage.Id).ToListAsync();
        periods.Should().HaveCount(1);
        var period = periods[0];
        period.Key.Should().Be("ordinario");
        period.IsDefault.Should().BeTrue();
        period.SlotsPerDay.Should().Be(6);

        // Verify cycles created
        var cycles = await db.CycleSchedules.Where(c => c.PeriodId == period.Id).ToListAsync();
        cycles.Should().HaveCount(3);
        cycles.Should().Contain(c => c.Cycle == 1);
        cycles.Should().Contain(c => c.Cycle == 2);
        cycles.Should().Contain(c => c.Cycle == 3);
    }

    [Fact]
    public async Task Handle_RemoveStageWithoutDependencies_DeletesCascadingly()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
            Stage = "primaria,secundaria"
        };
        db.Schools.Add(school);

        var primariaStage = new SchoolStage { SchoolId = SchoolId, StageType = StageTypes.Primaria, Name = "Educación Primaria" };
        var secundariaStage = new SchoolStage { SchoolId = SchoolId, StageType = StageTypes.Secundaria, Name = "Educación Secundaria" };
        db.SchoolStages.AddRange(primariaStage, secundariaStage);

        var period = new SchoolPeriod { SchoolId = SchoolId, StageId = secundariaStage.Id, Key = "ordinario", Name = "Jornada ordinaria" };
        db.SchoolPeriods.Add(period);

        var cycle = new CycleSchedule { SchoolId = SchoolId, StageId = secundariaStage.Id, PeriodId = period.Id, Cycle = 1 };
        db.CycleSchedules.Add(cycle);

        var cycleBreak = new CycleBreak { CycleScheduleId = cycle.Id, AfterSlot = 2, Minutes = 30 };
        db.CycleBreaks.Add(cycleBreak);

        await db.SaveChangesAsync();

        var repo = new FakeSchoolRepository(db) { CurrentSchool = school };
        var user = new FakeCurrentUser(SchoolId);
        var handler = new UpdateSchoolHandler(db, repo, user);

        var command = new UpdateSchoolCommand(
            Name: null, CenterCode: null, Locality: null, Community: null,
            Stage: "primaria",
            MinCourseLevel: null, MaxCourseLevel: null, AcademicYear: null,
            ScheduleType: null, MorningStart: null, SlotMinutes: null,
            BreakAfterSlot: null, BreakMinutes: null, SlotsPerDay: null, AfternoonSlots: null,
            AfternoonStart: null, WorkingDays: null);

        await handler.Handle(command, CancellationToken.None);

        school.Stage.Should().Be("primaria");

        var stages = await db.SchoolStages.Where(st => st.SchoolId == SchoolId).ToListAsync();
        stages.Should().HaveCount(1);
        stages.Should().NotContain(st => st.StageType == StageTypes.Secundaria);

        var periods = await db.SchoolPeriods.Where(p => p.StageId == secundariaStage.Id).ToListAsync();
        periods.Should().BeEmpty();

        var cycles = await db.CycleSchedules.Where(c => c.StageId == secundariaStage.Id).ToListAsync();
        cycles.Should().BeEmpty();

        var breaks = await db.CycleBreaks.Where(b => b.CycleScheduleId == cycle.Id).ToListAsync();
        breaks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RemoveStageWithDependencies_ThrowsInvalidOperationException()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
            Stage = "primaria,secundaria"
        };
        db.Schools.Add(school);

        var primariaStage = new SchoolStage { SchoolId = SchoolId, StageType = StageTypes.Primaria, Name = "Educación Primaria" };
        var secundariaStage = new SchoolStage { SchoolId = SchoolId, StageType = StageTypes.Secundaria, Name = "Educación Secundaria" };
        db.SchoolStages.AddRange(primariaStage, secundariaStage);

        // Add a group to secundaria (restrictive dependency)
        var group = new CourseGroup { SchoolId = SchoolId, StageId = secundariaStage.Id, CourseLevel = 1, GroupLabel = "A" };
        db.CourseGroups.Add(group);

        await db.SaveChangesAsync();

        var repo = new FakeSchoolRepository(db) { CurrentSchool = school };
        var user = new FakeCurrentUser(SchoolId);
        var handler = new UpdateSchoolHandler(db, repo, user);

        var command = new UpdateSchoolCommand(
            Name: null, CenterCode: null, Locality: null, Community: null,
            Stage: "primaria",
            MinCourseLevel: null, MaxCourseLevel: null, AcademicYear: null,
            ScheduleType: null, MorningStart: null, SlotMinutes: null,
            BreakAfterSlot: null, BreakMinutes: null, SlotsPerDay: null, AfternoonSlots: null,
            AfternoonStart: null, WorkingDays: null);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se puede eliminar la etapa 'secundaria'*");

        // Verify nothing was deleted
        var stages = await db.SchoolStages.Where(st => st.SchoolId == SchoolId).ToListAsync();
        stages.Should().HaveCount(2);
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
        public string Role => "school_admin";
        public bool IsAdmin => true;
        public bool IsTeacher => false;
    }
}
