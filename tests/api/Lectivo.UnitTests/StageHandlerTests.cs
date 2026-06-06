using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Schools;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class StageHandlerTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

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

    [Fact]
    public async Task CreateStage_CreatesStagePeriodAndCycles()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
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

        var user = new FakeCurrentUser(SchoolId);
        var handler = new CreateStageHandler(db, user);

        var result = await handler.Handle(new CreateStageCommand("secundaria"), CancellationToken.None);

        result.StageType.Should().Be("secundaria");
        result.Name.Should().Be("Educación Secundaria (ESO)");
        result.MinLevel.Should().Be(1);
        result.MaxLevel.Should().Be(4);

        var stages = await db.SchoolStages.Where(st => st.SchoolId == SchoolId).ToListAsync();
        stages.Should().HaveCount(2);
        stages.Should().Contain(st => st.StageType == StageTypes.Secundaria);

        var secStage = stages.First(st => st.StageType == StageTypes.Secundaria);
        secStage.Name.Should().Be("Educación Secundaria (ESO)");
        secStage.MinLevel.Should().Be(1);
        secStage.MaxLevel.Should().Be(4);
        secStage.SlotsPerDay.Should().Be(6);

        var periods = await db.SchoolPeriods.Where(p => p.StageId == secStage.Id).ToListAsync();
        periods.Should().HaveCount(1);
        var period = periods[0];
        period.Key.Should().Be("ordinario");
        period.IsDefault.Should().BeTrue();
        period.SlotsPerDay.Should().Be(6);

        var cycles = await db.CycleSchedules.Where(c => c.PeriodId == period.Id).ToListAsync();
        cycles.Should().HaveCount(3);
        cycles.Should().Contain(c => c.Cycle == 1);
        cycles.Should().Contain(c => c.Cycle == 2);
        cycles.Should().Contain(c => c.Cycle == 3);
    }

    [Fact]
    public async Task CreateStage_Duplicate_ThrowsInvalidOperationException()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
        };
        db.Schools.Add(school);
        db.SchoolStages.Add(new SchoolStage
        {
            SchoolId = SchoolId,
            StageType = StageTypes.Primaria,
            Name = "Educación Primaria"
        });
        await db.SaveChangesAsync();

        var user = new FakeCurrentUser(SchoolId);
        var handler = new CreateStageHandler(db, user);

        var act = async () => await handler.Handle(new CreateStageCommand("primaria"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ya existe*");
    }

    [Fact]
    public async Task DeleteStage_WithoutDependencies_DeletesCascadingly()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
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

        var user = new FakeCurrentUser(SchoolId);
        var handler = new DeleteStageHandler(db, user);

        await handler.Handle(new DeleteStageCommand(secundariaStage.Id), CancellationToken.None);

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
    public async Task DeleteStage_WithDependencies_ThrowsInvalidOperationException()
    {
        await using var db = CreateDb();
        var school = new School
        {
            Id = SchoolId,
            Name = "CEIP Test",
            Slug = "ceip-test",
        };
        db.Schools.Add(school);

        var primariaStage = new SchoolStage { SchoolId = SchoolId, StageType = StageTypes.Primaria, Name = "Educación Primaria" };
        var secundariaStage = new SchoolStage { SchoolId = SchoolId, StageType = StageTypes.Secundaria, Name = "Educación Secundaria" };
        db.SchoolStages.AddRange(primariaStage, secundariaStage);

        var group = new CourseGroup { SchoolId = SchoolId, StageId = secundariaStage.Id, CourseLevel = 1, GroupLabel = "A" };
        db.CourseGroups.Add(group);

        await db.SaveChangesAsync();

        var user = new FakeCurrentUser(SchoolId);
        var handler = new DeleteStageHandler(db, user);

        var act = async () => await handler.Handle(new DeleteStageCommand(secundariaStage.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se puede eliminar la etapa 'secundaria'*");

        var stages = await db.SchoolStages.Where(st => st.SchoolId == SchoolId).ToListAsync();
        stages.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteStage_NotFound_ThrowsNotFoundException()
    {
        await using var db = CreateDb();
        var user = new FakeCurrentUser(SchoolId);
        var handler = new DeleteStageHandler(db, user);

        var act = async () => await handler.Handle(new DeleteStageCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
