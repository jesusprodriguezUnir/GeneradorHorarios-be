using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Schools;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Domain.Schools;
using HorariosEscolares.Infrastructure.Persistence;

namespace Lectivo.UnitTests;

public class SchoolHandlersTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid StageId = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static School CreateSchool() => new()
    {
        Id = SchoolId,
        Name = "CEIP Test",
        Slug = "ceip-test",
        WorkingDays = "[1,2,3,4,5]",
        SlotsPerDay = 5,
        DaysPerWeek = 5,
        MorningStart = new TimeOnly(9, 0),
        SlotMinutes = 60,
        BreakAfterSlot = 2,
        BreakMinutes = 30,
        MinCourseLevel = 1,
        MaxCourseLevel = 6,
    };

    private static SchoolStage CreateStage() => new()
    {
        Id = StageId,
        SchoolId = SchoolId,
        StageType = StageTypes.Primaria,
        Name = "Educación Primaria",
        MinLevel = 1,
        MaxLevel = 6,
        SortOrder = 0,
        WorkingDays = "[1,2,3,4,5]",
        SlotsPerDay = 5,
        MorningStart = new TimeOnly(9, 0),
        SlotMinutes = 60,
        BreakAfterSlot = 2,
        BreakMinutes = 30,
    };

    private static SchoolPeriod CreatePeriod() => new()
    {
        Id = PeriodId,
        SchoolId = SchoolId,
        StageId = StageId,
        Key = "ordinario",
        Name = "Jornada ordinaria",
        Months = "[10,11,12,1,2,3,4,5]",
        ScheduleType = "continua",
        SlotMinutes = 60,
        SlotsPerDay = 5,
        AfternoonSlots = 0,
        IsDefault = true,
        SortOrder = 0,
    };

    private static void SeedSchoolWithPeriod(AppDbContext db)
    {
        var school = CreateSchool();
        var stage = CreateStage();
        var period = CreatePeriod();
        period.Cycles.Add(new CycleSchedule
        {
            SchoolId = SchoolId,
            StageId = StageId,
            PeriodId = period.Id,
            Cycle = 1,
            MorningStart = new TimeOnly(9, 0),
            EndTime = new TimeOnly(14, 0),
        });
        db.Schools.Add(school);
        db.SchoolStages.Add(stage);
        db.SchoolPeriods.Add(period);
        db.SaveChanges();
    }

    [Fact]
    public async Task GetSchoolHandler_ReturnsMappedDto()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetSchoolHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetSchoolQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(SchoolId);
        result.Name.Should().Be("CEIP Test");
        result.Cycles.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSchoolHandler_WrongSchool_ReturnsNull()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetSchoolHandler(db, new FakeCurrentUser(Guid.NewGuid()));
        var result = await handler.Handle(new GetSchoolQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStagesHandler_ReturnsStagesForSchool()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetStagesHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetStagesQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].StageType.Should().Be(StageTypes.Primaria);
    }

    [Fact]
    public async Task GetCycleScheduleHandler_ReturnsCycle()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetCycleScheduleHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetCycleScheduleQuery(1), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Cycle.Should().Be(1);
    }

    [Fact]
    public async Task GetPeriodsHandler_ReturnsPeriods()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetPeriodsHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetPeriodsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("ordinario");
    }

    [Fact]
    public async Task GetPeriodHandler_ReturnsPeriod()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetPeriodHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetPeriodQuery(PeriodId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(PeriodId);
    }

    [Fact]
    public async Task GetPeriodCycleHandler_ReturnsCycle()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new GetPeriodCycleHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new GetPeriodCycleQuery(PeriodId, 1), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Cycle.Should().Be(1);
    }

    [Fact]
    public async Task UpdateCycleScheduleHandler_UpdatesCycleBoundaries()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new UpdateCycleScheduleHandler(db, new FakeCurrentUser(SchoolId), new FakeCycleScheduleRepository(db));
        var result = await handler.Handle(new UpdateCycleScheduleCommand(
            1, "08:30", "14:00", AfternoonStart: null, AfternoonEnd: null), CancellationToken.None);

        result.MorningStart.Should().Be("08:30");
        result.MorningEnd.Should().Be("14:00");
    }

    [Fact]
    public async Task CreatePeriodHandler_CreatesPeriodWithCycles()
    {
        await using var db = CreateDb();
        db.Schools.Add(CreateSchool());
        db.SchoolStages.Add(CreateStage());
        await db.SaveChangesAsync();

        var handler = new CreatePeriodHandler(db, new FakeSchoolPeriodRepository(db), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new CreatePeriodCommand(
            StageId, "extra", "Periodo extra", [1, 2, 3], "continua", 60, 5, 0, 1), CancellationToken.None);

        result.Key.Should().Be("extra");
        result.Cycles.Should().HaveCount(3);

        var periods = await db.SchoolPeriods.Where(p => p.StageId == StageId).ToListAsync();
        periods.Should().Contain(p => p.Key == "extra");
    }

    [Fact]
    public async Task DeletePeriodHandler_DeletesNonDefaultPeriod()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);
        var extraPeriod = new SchoolPeriod
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            StageId = StageId,
            Key = "extra",
            Name = "Extra",
            Months = "[1]",
            ScheduleType = "continua",
            SlotMinutes = 60,
            SlotsPerDay = 5,
            AfternoonSlots = 0,
            IsDefault = false,
            SortOrder = 1,
        };
        db.SchoolPeriods.Add(extraPeriod);
        await db.SaveChangesAsync();

        var handler = new DeletePeriodHandler(new FakeSchoolPeriodRepository(db), new FakeCurrentUser(SchoolId));
        await handler.Handle(new DeletePeriodCommand(extraPeriod.Id), CancellationToken.None);

        var period = await db.SchoolPeriods.FindAsync(extraPeriod.Id);
        period.Should().BeNull();
    }

    [Fact]
    public async Task DeletePeriodHandler_DefaultPeriod_Throws()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new DeletePeriodHandler(new FakeSchoolPeriodRepository(db), new FakeCurrentUser(SchoolId));
        var act = async () => await handler.Handle(new DeletePeriodCommand(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePeriodCycleHandler_CreatesCycleIfMissing()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new UpdatePeriodCycleHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new UpdatePeriodCycleCommand(
            PeriodId, 2, "09:00", "14:00"), CancellationToken.None);

        result.Cycle.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStageHandler_UpdatesStage()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new UpdateStageHandler(db, new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new UpdateStageCommand(
            StageId, Name: "Primaria actualizada"), CancellationToken.None);

        result.Name.Should().Be("Primaria actualizada");
    }

    [Fact]
    public async Task UpdatePeriodHandler_UpdatesPeriod()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new UpdatePeriodHandler(db, new FakeSchoolPeriodRepository(db), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new UpdatePeriodCommand(
            PeriodId, Name: "Periodo actualizado", Months: [1, 2, 3],
            ScheduleType: null, SlotMinutes: null, SlotsPerDay: null, AfternoonSlots: null, SortOrder: null),
            CancellationToken.None);

        result.Name.Should().Be("Periodo actualizado");
        result.Months.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task NormativeCheckHandler_ReturnsCompliantResult()
    {
        await using var db = CreateDb();
        SeedSchoolWithPeriod(db);

        var handler = new NormativeCheckHandler(db, new FakeNormativeValidator(), new FakeCurrentUser(SchoolId));
        var result = await handler.Handle(new NormativeCheckQuery(), CancellationToken.None);

        result.IsCompliant.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    private sealed class FakeNormativeValidator : INormativeValidator
    {
        public Task<List<ConflictExplanation>> ValidateAsync(NormativeValidationData data, CancellationToken ct)
            => Task.FromResult(new List<ConflictExplanation>());
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

    private sealed class FakeCycleScheduleRepository(AppDbContext db) : ICycleScheduleRepository
    {
        public Task<CycleSchedule?> GetBySchoolAndCycleAsync(Guid schoolId, int cycle, CancellationToken ct, bool includeBreaks = true)
        {
            var query = db.CycleSchedules.AsQueryable();
            if (includeBreaks) query = query.Include(c => c.Breaks);
            return query.FirstOrDefaultAsync(c => c.SchoolId == schoolId && c.Cycle == cycle, ct);
        }

        public Task<CycleSchedule?> GetByPeriodAndCycleAsync(Guid periodId, int cycle, CancellationToken ct, bool includeBreaks = true)
        {
            var query = db.CycleSchedules.AsQueryable();
            if (includeBreaks) query = query.Include(c => c.Breaks);
            return query.FirstOrDefaultAsync(c => c.PeriodId == periodId && c.Cycle == cycle, ct);
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    private sealed class FakeSchoolPeriodRepository(AppDbContext db) : ISchoolPeriodRepository
    {
        public Task<SchoolPeriod?> GetByIdAsync(Guid id, CancellationToken ct, bool includeCycles = true)
        {
            var query = db.SchoolPeriods.AsQueryable();
            if (includeCycles) query = query.Include(p => p.Cycles).ThenInclude(c => c.Breaks);
            return query.FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public Task<List<SchoolPeriod>> GetBySchoolAsync(Guid schoolId, CancellationToken ct)
            => db.SchoolPeriods.Where(p => p.SchoolId == schoolId).ToListAsync(ct);

        public Task AddAsync(SchoolPeriod period, CancellationToken ct)
        {
            db.SchoolPeriods.Add(period);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

        public void Delete(SchoolPeriod period) => db.SchoolPeriods.Remove(period);
    }
}
