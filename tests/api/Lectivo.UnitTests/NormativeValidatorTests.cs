using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Infrastructure.Normative;
using HorariosEscolares.Infrastructure.Persistence.Entities;
using FluentAssertions;

namespace Lectivo.UnitTests;

/// <summary>
/// Tests unitarios del validador normativo (Decreto 61/2022 Madrid).
/// No requieren base de datos — trabajan con entidades en memoria.
/// </summary>
public class NormativeValidatorTests
{
    private static readonly NormativeValidator Validator = new();

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Crea un School con configuración mínima válida.</summary>
    private static School MakeSchool(
        string stage            = "primaria",
        int breakMinutes        = 30,
        int slotsPerDay         = 5,
        int slotMinutes         = 60,
        int daysPerWeek         = 5,
        int minCourseLevel      = 1,
        int maxCourseLevel      = 6,
        string scheduleType     = "continua") =>
        new()
        {
            Id             = Guid.NewGuid(),
            Name           = "CEIP Test",
            Slug           = "test",
            Stage          = stage,
            BreakMinutes   = breakMinutes,
            SlotsPerDay    = slotsPerDay,
            SlotMinutes    = slotMinutes,
            DaysPerWeek    = daysPerWeek,
            MinCourseLevel = minCourseLevel,
            MaxCourseLevel = maxCourseLevel,
            ScheduleType   = scheduleType,
            MorningStart   = new TimeOnly(9, 0),
            WorkingDays    = "[1,2,3,4,5]",
        };

    /// <summary>Crea una asignación con su SubjectAllocation correspondiente.</summary>
    private static (Assignment, SubjectAllocation) MakeAssignment(
        Guid groupId, string subjectKey, int weeklyHours,
        int minH = 1, int maxH = 6)
    {
        var alloc = new SubjectAllocation
        {
            Id                   = Guid.NewGuid(),
            TemplateId           = Guid.NewGuid(),
            SubjectName          = subjectKey,
            SubjectShort         = subjectKey,
            SubjectKey           = subjectKey,
            WeeklyHoursMin       = minH,
            WeeklyHoursMax       = maxH,
            WeeklyHoursDefault   = weeklyHours,
            RequiresSpecialist   = false,
            MaxConsecutiveSlots  = 2,
            SplittableAcrossDays = true,
        };
        var assignment = new Assignment
        {
            Id           = Guid.NewGuid(),
            SchoolId     = Guid.NewGuid(),
            TeacherId    = Guid.NewGuid(),
            GroupId      = groupId,
            AllocationId = alloc.Id,
            WeeklyHours  = weeklyHours,
        };
        return (assignment, alloc);
    }

    /// <summary>
    /// Construye una lista de asignaciones estándar (24 h/semana)
    /// para el grupo indicado.
    /// </summary>
    private static IReadOnlyList<(Assignment, SubjectAllocation)> StandardGroupAssignments(Guid groupId)
    {
        return
        [
            MakeAssignment(groupId, "len", 5, minH: 4, maxH: 6),
            MakeAssignment(groupId, "mat", 5, minH: 4, maxH: 6),
            MakeAssignment(groupId, "cie", 3, minH: 3, maxH: 4),
            MakeAssignment(groupId, "ing", 4, minH: 3, maxH: 5),
            MakeAssignment(groupId, "ef",  3, minH: 2, maxH: 3),
            MakeAssignment(groupId, "mus", 1, minH: 1, maxH: 2),
            MakeAssignment(groupId, "art", 2, minH: 1, maxH: 2),
            MakeAssignment(groupId, "rel", 1, minH: 1, maxH: 2),
        ];
    }

    // ── Tests de configuración del colegio ────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_ValidConfig_ReturnsNoIssues()
    {
        var school = MakeSchool();
        var groupId = Guid.NewGuid();
        var data = StandardGroupAssignments(groupId);

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_BreakLessThan30Min_ReturnsError()
    {
        var school = MakeSchool(breakMinutes: 20);
        var groupId = Guid.NewGuid();
        var data = StandardGroupAssignments(groupId);

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().ContainSingle(i =>
            i.Severity == ConflictSeverity.Error &&
            i.Type == ConflictType.Normative &&
            i.Description.Contains("20 min"));
    }

    [Fact]
    public async Task ValidateAsync_ExactlyLegalBreak_NoBreakError()
    {
        var school = MakeSchool(breakMinutes: 30);
        var groupId = Guid.NewGuid();
        var data = StandardGroupAssignments(groupId);

        var issues = await Validator.ValidateAsync(school, data);
        issues.Should().NotContain(i => i.Description.Contains("recreo"));
    }

    [Fact]
    public async Task ValidateAsync_GridBelowMinimum_ReturnsError()
    {
        // 3 slots × 60 min × 5 días = 15 h < 22.5 h mínimo
        var school = MakeSchool(slotsPerDay: 3, slotMinutes: 60, daysPerWeek: 5);
        var groupId = Guid.NewGuid();
        var data = StandardGroupAssignments(groupId);

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().Contain(i =>
            i.Severity == ConflictSeverity.Error &&
            i.Description.Contains("15,0 h/semana"));
    }

    [Fact]
    public async Task ValidateAsync_MaxLevelAbove6_ReturnsError()
    {
        var school = MakeSchool(maxCourseLevel: 7);
        var data = Array.Empty<(Assignment, SubjectAllocation)>();

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().Contain(i =>
            i.Severity == ConflictSeverity.Error &&
            i.Description.Contains("nivel máximo"));
    }

    [Fact]
    public async Task ValidateAsync_NonPrimariaStage_ReturnsWarning()
    {
        var school = MakeSchool(stage: "secundaria");
        var data = Array.Empty<(Assignment, SubjectAllocation)>();

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().Contain(i =>
            i.Severity == ConflictSeverity.Warning &&
            i.Description.Contains("secundaria"));
    }

    // ── Tests de horas por grupo ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_GroupBelowMinimumHours_ReturnsError()
    {
        var school = MakeSchool();
        var groupId = Guid.NewGuid();
        // Solo asignamos 10 h (< 22,5 mínimo)
        var data = new[]
        {
            MakeAssignment(groupId, "len", 5),
            MakeAssignment(groupId, "mat", 5),
        };

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().Contain(i =>
            i.Severity == ConflictSeverity.Error &&
            i.GroupId == groupId &&
            i.Description.Contains("10 h/semana"));
    }

    [Fact]
    public async Task ValidateAsync_GroupExceedsGridCapacity_ReturnsError()
    {
        var school = MakeSchool(slotsPerDay: 5, slotMinutes: 60, daysPerWeek: 5); // capacidad 25h
        var groupId = Guid.NewGuid();
        // Asignamos 28 h (> 25 h capacidad)
        var data = new[]
        {
            MakeAssignment(groupId, "len", 8, minH: 4, maxH: 10),
            MakeAssignment(groupId, "mat", 8, minH: 4, maxH: 10),
            MakeAssignment(groupId, "cie", 7, minH: 3, maxH: 10),
            MakeAssignment(groupId, "ing", 5, minH: 3, maxH: 6),
        };

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().Contain(i =>
            i.Severity == ConflictSeverity.Error &&
            i.GroupId == groupId &&
            i.Description.Contains("capacidad"));
    }

    [Fact]
    public async Task ValidateAsync_SubjectBelowMinimum_ReturnsWarning()
    {
        var school = MakeSchool();
        var groupId = Guid.NewGuid();
        // Inglés con solo 2 h (< 3 h mínimo estándar)
        var data = new List<(Assignment, SubjectAllocation)>(StandardGroupAssignments(groupId));
        // Substituir la asignación de inglés por una con 2 h
        data = data.Where(d => d.Item2.SubjectKey != "ing").ToList();
        data.Add(MakeAssignment(groupId, "ing", 2, minH: 3, maxH: 5));

        var issues = await Validator.ValidateAsync(school, data);

        issues.Should().Contain(i =>
            i.Severity == ConflictSeverity.Warning &&
            i.GroupId == groupId &&
            i.Description.Contains("Inglés"));
    }

    [Fact]
    public async Task ValidateAsync_FullStandardSeed_IsCompliant()
    {
        // Simula exactamente el seed demo: 18 grupos estándar
        var school = MakeSchool();
        var data = new List<(Assignment, SubjectAllocation)>();

        for (int i = 0; i < 18; i++)
        {
            var gId = Guid.NewGuid();
            data.AddRange(StandardGroupAssignments(gId));
        }

        var issues = await Validator.ValidateAsync(school, data);

        // No debe haber ningún error
        var errors = issues.Where(i => i.Severity == ConflictSeverity.Error).ToList();
        errors.Should().BeEmpty("el seed demo debe cumplir la normativa sin errores");
    }

    // ── Tests de LomloeMadrid (datos normativos) ──────────────────────────────────

    [Fact]
    public void LomloeMadrid_StandardSubjects_HasRequiredAreas()
    {
        var keys = LomloeMadrid.StandardSubjects.Select(s => s.SubjectKey).ToHashSet();
        keys.Should().Contain(["len", "mat", "cie", "ing", "ef", "mus", "art", "rel", "tut"]);
    }

    [Fact]
    public void LomloeMadrid_BilingueSubjects_HasHigherInglesHours()
    {
        var std = LomloeMadrid.StandardSubjects.First(s => s.SubjectKey == "ing");
        var bil = LomloeMadrid.BilingueSubjects.First(s => s.SubjectKey == "ing");

        bil.DefaultH.Should().BeGreaterThan(std.DefaultH);
        bil.MinH.Should().BeGreaterOrEqualTo(std.MinH);
    }

    [Fact]
    public void LomloeMadrid_MinWeeklyLectiveHours_Is22Point5()
    {
        LomloeMadrid.MinWeeklyLectiveHours.Should().Be(22.5m);
    }

    [Fact]
    public void LomloeMadrid_MinDailyBreakMinutes_Is30()
    {
        LomloeMadrid.MinDailyBreakMinutes.Should().Be(30);
    }

    [Fact]
    public void LomloeMadrid_GetSubjects_ReturnsCorrectModalityData()
    {
        LomloeMadrid.GetSubjects("estandar").Should().BeSameAs(LomloeMadrid.StandardSubjects);
        LomloeMadrid.GetSubjects("bilingue").Should().BeSameAs(LomloeMadrid.BilingueSubjects);
        LomloeMadrid.GetSubjects("BILINGUE").Should().BeSameAs(LomloeMadrid.BilingueSubjects);
        LomloeMadrid.GetSubjects("otro").Should().BeSameAs(LomloeMadrid.StandardSubjects);
    }

    [Fact]
    public void LomloeMadrid_AllSubjectMinH_LessThanOrEqualMaxH()
    {
        foreach (var s in LomloeMadrid.StandardSubjects.Concat(LomloeMadrid.BilingueSubjects))
        {
            s.MinH.Should().BeLessOrEqualTo(s.MaxH, $"área '{s.SubjectKey}' tiene Min > Max");
            s.DefaultH.Should().BeInRange(s.MinH, s.MaxH,
                $"el DefaultH de '{s.SubjectKey}' debe estar entre Min y Max");
        }
    }
}
