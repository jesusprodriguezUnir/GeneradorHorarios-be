using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using FluentAssertions;

namespace Lectivo.UnitTests;

public class ScheduleViabilityAnalyzerTests
{
    private static readonly HashSet<(Guid, int, int)> NoUnavailable = [];

    // ── Caso factible ─────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_ReturnsNoConflicts_WhenConfigurationIsViable()
    {
        var school = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5); // 25 slots lectivos
        var sessions = Enumerable.Range(0, 10)
            .Select(_ => TestData.Session())
            .ToList();

        var result = ScheduleViabilityAnalyzer.Analyze(school, sessions, NoUnavailable);

        result.Should().BeEmpty();
    }

    // ── Check 1: Especialista ausente ─────────────────────────────────────────

    [Fact]
    public void Analyze_ReturnsError_WhenTeacherLacksRequiredSpecialty()
    {
        var school = TestData.DefaultSchool();
        var session = TestData.Session(
            requiresSpecialist: true,
            subjectKey: "ing",
            subjectName: "Inglés",
            teacherSpecialties: ["Generalista"]); // no tiene Inglés

        var result = ScheduleViabilityAnalyzer.Analyze(school, [session], NoUnavailable);

        result.Should().ContainSingle()
            .Which.Type.Should().Be(ConflictType.Teacher);
        result[0].Severity.Should().Be(ConflictSeverity.Error);
        result[0].TeacherId.Should().Be(session.TeacherId);
    }

    [Fact]
    public void Analyze_ReturnsNoError_WhenTeacherHasRequiredSpecialty()
    {
        var school = TestData.DefaultSchool();
        var session = TestData.Session(
            requiresSpecialist: true,
            subjectKey: "mus",
            subjectName: "Música",
            teacherSpecialties: ["Música", "Generalista"]);

        var result = ScheduleViabilityAnalyzer.Analyze(school, [session], NoUnavailable);

        result.Should().BeEmpty();
    }

    // ── Check 2: Tipo de aula inexistente ─────────────────────────────────────

    [Fact]
    public void Analyze_ReturnsError_WhenRequiredClassroomTypeAbsent()
    {
        // Escuela sin aulas IT
        var school = TestData.DefaultSchool();
        var session = TestData.Session(
            requiredClassroomType: ClassroomType.IT,
            subjectName: "Informática");

        var result = ScheduleViabilityAnalyzer.Analyze(school, [session], NoUnavailable);

        result.Should().ContainSingle()
            .Which.Type.Should().Be(ConflictType.Classroom);
        result[0].Severity.Should().Be(ConflictSeverity.Error);
    }

    // ── Check 3: Capacidad por grupo ──────────────────────────────────────────

    [Fact]
    public void Analyze_ReturnsError_WhenGroupHasMoreSessionsThanSlots()
    {
        // 5 días × 5 slots = 25 slots lectivos; 26 sesiones → imposible
        var school = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5);
        var groupId = TestData.Group1Id;
        var sessions = Enumerable.Range(0, 26)
            .Select(_ => TestData.Session(groupId: groupId))
            .ToList();

        var result = ScheduleViabilityAnalyzer.Analyze(school, sessions, NoUnavailable);

        result.Should().ContainSingle(c => c.Type == ConflictType.Coverage)
            .Which.GroupId.Should().Be(groupId);
        result[0].Severity.Should().Be(ConflictSeverity.Error);
    }

    [Fact]
    public void Analyze_ReturnsNoError_WhenGroupSessionsFitExactly()
    {
        // Exactamente 25 sesiones para 25 slots disponibles
        var school = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5);
        var sessions = Enumerable.Range(0, 25)
            .Select(_ => TestData.Session())
            .ToList();

        var result = ScheduleViabilityAnalyzer.Analyze(school, sessions, NoUnavailable);

        result.Should().BeEmpty();
    }

    // ── Check 4: Demanda por profesor ─────────────────────────────────────────

    [Fact]
    public void Analyze_ReturnsError_WhenTeacherHasMoreSessionsThanAvailableSlots()
    {
        // Profesor con maxWeeklyHours=3 pero tiene 5 sesiones asignadas
        var school = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5);
        var teacherId = TestData.Teacher1Id;
        var sessions = Enumerable.Range(0, 5)
            .Select(_ => TestData.Session(teacherId: teacherId, teacherMaxWeeklyHours: 3))
            .ToList();

        var result = ScheduleViabilityAnalyzer.Analyze(school, sessions, NoUnavailable);

        result.Should().ContainSingle(c => c.Type == ConflictType.Teacher && c.TeacherId == teacherId)
            .Which.Severity.Should().Be(ConflictSeverity.Error);
    }

    [Fact]
    public void Analyze_ReturnsError_WhenTeacherUnavailabilityLeavesNoSlots()
    {
        // Profesor con 3 sesiones pero bloqueado en todos los slots lectivos (5×5=25 bloqueados)
        var school = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5);
        var teacherId = TestData.Teacher1Id;
        var sessions = Enumerable.Range(0, 3)
            .Select(_ => TestData.Session(teacherId: teacherId, teacherMaxWeeklyHours: 25))
            .ToList();

        // Bloquear todos los slots del profesor
        var unavailable = school.WorkingDays
            .SelectMany(day => school.Slots.Select(s => (teacherId, day, s.Index)))
            .ToHashSet();

        var result = ScheduleViabilityAnalyzer.Analyze(school, sessions, unavailable);

        result.Should().ContainSingle(c => c.Type == ConflictType.Teacher && c.TeacherId == teacherId);
    }

    // ── Check 5: Capacidad de aulas especiales ────────────────────────────────

    [Fact]
    public void Analyze_ReturnsError_WhenSpecialClassroomDemandExceedsSupply()
    {
        // DefaultSchool tiene 1 gimnasio. 5 días × 5 slots = 25 slots.
        // 26 sesiones de EF que requieren Gym → imposible.
        var school = TestData.DefaultSchool(slotsPerDay: 5, daysPerWeek: 5);
        var gymId = Guid.NewGuid();
        var sessions = Enumerable.Range(0, 26)
            .Select(_ => TestData.Session(
                groupId: Guid.NewGuid(), // grupos distintos para no disparar check 3
                requiredClassroomType: ClassroomType.Gym,
                subjectName: "Ed. Física"))
            .ToList();

        var result = ScheduleViabilityAnalyzer.Analyze(school, sessions, NoUnavailable);

        result.Should().Contain(c => c.Type == ConflictType.Classroom && c.Severity == ConflictSeverity.Error);
    }
}
