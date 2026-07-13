using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HorariosEscolares.Application.Common.Exceptions;
using HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;
using HorariosEscolares.Domain.Constraints;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Scheduling;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Repositories;

namespace Lectivo.UnitTests;

public class GenerateScheduleOrchestratorTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid StageId = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid AllocationId = Guid.NewGuid();
    private static readonly Guid ClassroomId = Guid.NewGuid();

    private static void SeedStage(AppDbContext db) =>
        db.SchoolStages.Add(new SchoolStage
        {
            Id = StageId,
            SchoolId = SchoolId,
            StageType = StageTypes.Primaria,
            Name = "Educación Primaria",
            MinLevel = 1,
            MaxLevel = 6,
            SlotsPerDay = 5,
            BreakMinutes = 30,
            WorkingDays = "[1,2,3,4,5]",
        });

    private static School CreateSchool() => new()
    {
        Id = SchoolId,
        Name = "Test School",
        Slug = "test-school",
        SlotsPerDay = 5,
        DaysPerWeek = 5,
        WorkingDays = "[1,2,3,4,5]",
        MorningStart = new TimeOnly(9, 0),
        SlotMinutes = 60,
        BreakAfterSlot = 2,
        BreakMinutes = 30,
        MinCourseLevel = 1,
        MaxCourseLevel = 6,
    };

    private static void SeedDefaultPeriod(AppDbContext db)
    {
        var period = new SchoolPeriod
        {
            Id = PeriodId,
            SchoolId = SchoolId,
            StageId = StageId,
            Key = "ordinario",
            Name = "Test ordinario",
            Months = "[10,11,12,1,2,3,4,5]",
            ScheduleType = "continua",
            SlotMinutes = 60,
            SlotsPerDay = 5,
            AfternoonSlots = 0,
            IsDefault = true,
            SortOrder = 0,
        };
        for (int c = 1; c <= 3; c++)
        {
            period.Cycles.Add(new CycleSchedule
            {
                SchoolId = SchoolId,
                StageId = StageId,
                PeriodId = period.Id,
                Cycle = c,
                MorningStart = new TimeOnly(9, 0),
                EndTime = new TimeOnly(14, 0),
            });
        }
        db.SchoolPeriods.Add(period);
    }

    private static void SeedMinimalData(AppDbContext db)
    {
        db.Schools.Add(CreateSchool());
        SeedStage(db);
        SeedDefaultPeriod(db);
        db.Teachers.Add(new Teacher
        {
            Id = TeacherId,
            SchoolId = SchoolId,
            FullName = "Profesor Test",
            Email = "test@school.es",
            MaxWeeklyHours = 25,
        });
        db.TeacherSubjectHours.Add(new TeacherSubjectHour
        {
            TeacherId = TeacherId,
            SubjectKey = "len",
            WeeklyHours = 5,
        });
        db.Classrooms.Add(new Classroom
        {
            Id = ClassroomId,
            SchoolId = SchoolId,
            Name = "Aula 1",
            ClassroomType = "regular",
        });
        db.CourseGroups.Add(new CourseGroup
        {
            Id = GroupId,
            SchoolId = SchoolId,
            StageId = StageId,
            CourseLevel = 1,
            GroupLabel = "A",
        });
        db.SubjectAllocations.Add(new SubjectAllocation
        {
            Id = AllocationId,
            TemplateId = Guid.NewGuid(),
            SubjectName = "Lengua",
            SubjectKey = "len",
            WeeklyHoursMin = 4,
            WeeklyHoursMax = 6,
            WeeklyHoursDefault = 5,
            MaxConsecutiveSlots = 2,
            SplittableAcrossDays = true,
        });
        db.Assignments.Add(new Assignment
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 2,
        });
        db.SaveChanges();
    }

    private static void SeedMissingSpecialistData(AppDbContext db)
    {
        db.Schools.Add(CreateSchool());
        SeedStage(db);
        SeedDefaultPeriod(db);
        db.Teachers.Add(new Teacher
        {
            Id = TeacherId,
            SchoolId = SchoolId,
            FullName = "Profesor Test",
            Email = "test@school.es",
            MaxWeeklyHours = 25,
        });
        // Nota: NO se añade TeacherSubjectHour para "ing" → el viability check detectará la falta de especialidad.
        db.Classrooms.Add(new Classroom
        {
            Id = ClassroomId,
            SchoolId = SchoolId,
            Name = "Aula 1",
            ClassroomType = "regular",
        });
        db.CourseGroups.Add(new CourseGroup
        {
            Id = GroupId,
            SchoolId = SchoolId,
            StageId = StageId,
            CourseLevel = 1,
            GroupLabel = "A",
        });
        db.SubjectAllocations.Add(new SubjectAllocation
        {
            Id = AllocationId,
            TemplateId = Guid.NewGuid(),
            SubjectName = "Inglés",
            SubjectKey = "ing",
            WeeklyHoursMin = 3,
            WeeklyHoursMax = 5,
            WeeklyHoursDefault = 4,
            RequiresSpecialist = true,
            MaxConsecutiveSlots = 2,
            SplittableAcrossDays = true,
        });
        db.Assignments.Add(new Assignment
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            TeacherId = TeacherId,
            GroupId = GroupId,
            AllocationId = AllocationId,
            WeeklyHours = 2,
        });
        db.SaveChanges();
    }

    private static GenerateScheduleOrchestrator CreateOrchestrator(
        AppDbContext db,
        IScheduleRepository? repository = null,
        IScheduleEngine? engine = null,
        INormativeValidator? normativeValidator = null)
    {
        return new GenerateScheduleOrchestrator(
            db,
            new ScheduleResultPersister(db, repository ?? new FakeScheduleRepository()),
            engine ?? new BacktrackingScheduleEngine(),
            normativeValidator ?? new FakeNormativeValidator(),
            new CycleResolver());
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_SchoolNotFound_ThrowsNotFoundException()
    {
        await using var db = CreateInMemoryDb();
        var orchestrator = CreateOrchestrator(db);

        var act = async () => await orchestrator.GenerateAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2025/2026", Guid.Empty, 30, null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GenerateAsync_NoAssignments_ReturnsNoAssignments()
    {
        await using var db = CreateInMemoryDb();
        db.Schools.Add(CreateSchool());
        SeedStage(db);
        SeedDefaultPeriod(db);
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);
        var result = await orchestrator.GenerateAsync(
            SchoolId, StageId, PeriodId, "2025/2026", Guid.Empty, 30, null, CancellationToken.None);

        result.Should().BeOfType<GenerateScheduleResult.NoAssignments>();
    }

    [Fact]
    public async Task GenerateAsync_ViabilityFailed_ReturnsViabilityFailed()
    {
        await using var db = CreateInMemoryDb();
        SeedMissingSpecialistData(db);

        var orchestrator = CreateOrchestrator(db);
        var result = await orchestrator.GenerateAsync(
            SchoolId, StageId, PeriodId, "2025/2026", Guid.Empty, 30, null, CancellationToken.None);

        var failedResult = result.Should().BeOfType<GenerateScheduleResult.ViabilityFailed>().Subject;
        failedResult.TotalConflicts.Should().BeGreaterThan(0);
        failedResult.Conflicts.Should().Contain(c =>
            c.Type == ConflictType.Teacher &&
            c.Description.Contains("no tiene la especialidad requerida"));
    }

    [Fact]
    public async Task GenerateAsync_Success_ReturnsSuccessAndPersists()
    {
        await using var db = CreateInMemoryDb();
        SeedMinimalData(db);

        var fakeRepo = new FakeScheduleRepository();
        var orchestrator = CreateOrchestrator(db, fakeRepo);

        var result = await orchestrator.GenerateAsync(
            SchoolId, StageId, PeriodId, "2025/2026", Guid.Empty, 30, null, CancellationToken.None);

        var success = result.Should().BeOfType<GenerateScheduleResult.Success>().Subject;
        success.TotalAssigned.Should().Be(2);
        success.TotalRequired.Should().Be(2);
        success.Status.Should().Be("generated");

        fakeRepo.LastSchedule.Should().NotBeNull();
        fakeRepo.LastSchedule!.Status.Should().Be("generated");
        fakeRepo.LastEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateAsync_ViabilityFailed_PersistsFailedSchedule()
    {
        await using var db = CreateInMemoryDb();
        SeedMissingSpecialistData(db);

        var fakeRepo = new FakeScheduleRepository();
        var orchestrator = CreateOrchestrator(db, fakeRepo);

        await orchestrator.GenerateAsync(
            SchoolId, StageId, PeriodId, "2025/2026", Guid.Empty, 30, null, CancellationToken.None);

        fakeRepo.LastSchedule.Should().NotBeNull();
        fakeRepo.LastSchedule!.Status.Should().Be("failed");
        fakeRepo.LastEntries.Should().BeEmpty();
        fakeRepo.LastConflicts.Should().NotBeEmpty();
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public ScheduleRecord? LastSchedule { get; private set; }
        public List<ScheduleEntry> LastEntries { get; private set; } = [];
        public List<ScheduleConflictRecord> LastConflicts { get; private set; } = [];

        public Task AddScheduleWithDetailsAsync(
            ScheduleRecord schedule,
            IReadOnlyList<ScheduleEntry> entries,
            IReadOnlyList<ScheduleConflictRecord> conflicts,
            CancellationToken ct)
        {
            LastSchedule = schedule;
            LastEntries = [.. entries];
            LastConflicts = [.. conflicts];
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid scheduleId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeNormativeValidator : INormativeValidator
    {
        public Task<List<ConflictExplanation>> ValidateAsync(
            NormativeValidationData data, CancellationToken ct)
            => Task.FromResult(new List<ConflictExplanation>());
    }
}
