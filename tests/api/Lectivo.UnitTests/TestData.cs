using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Domain.Constraints;

namespace Lectivo.UnitTests;

/// <summary>
/// Builder de fixtures reutilizables para tests del motor de horarios.
/// </summary>
public static class TestData
{
    public static Guid SchoolId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static Guid Group1Id { get; } = Guid.Parse("00000000-0000-0000-0003-000000000001");
    public static Guid Teacher1Id { get; } = Guid.Parse("00000000-0000-0000-0001-000000000001");
    public static Guid Allocation1Id { get; } = Guid.Parse("00000000-0000-0000-0004-000000000001");
    public static Guid RegularClassroomId { get; } = Guid.Parse("00000000-0000-0000-0002-000000000001");
    public static Guid GymClassroomId { get; } = Guid.Parse("00000000-0000-0000-0002-000000000007");

    public static SchoolConfig DefaultSchool(int slotsPerDay = 5, int daysPerWeek = 5)
    {
        var slots = Enumerable.Range(0, slotsPerDay)
            .Select(i => new SlotConfig(i, IsBreak: false))
            .ToList();
        return new SchoolConfig(
            slotsPerDay,
            daysPerWeek,
            slots,
            new List<ClassroomInfo>
            {
                new(RegularClassroomId, "Aula 1A", ClassroomType.Regular),
                new(Guid.Parse("00000000-0000-0000-0002-000000000002"), "Aula 1B", ClassroomType.Regular),
                new(GymClassroomId, "Gimnasio", ClassroomType.Gym),
                new(Guid.Parse("00000000-0000-0000-0002-000000000008"), "Aula Música", ClassroomType.Music),
            });
    }

    public static SessionToAssign Session(
        Guid? assignmentId = null,
        Guid? groupId = null,
        Guid? teacherId = null,
        Guid? allocationId = null,
        string subjectName = "Lengua",
        string groupLabel = "1A",
        ClassroomType? requiredClassroomType = null,
        int maxConsecutiveSlots = 2,
        bool requiresSpecialist = false,
        string subjectKey = "",
        IReadOnlyList<string>? teacherSpecialties = null,
        int teacherMaxWeeklyHours = 25)
        => new(
            assignmentId ?? Guid.NewGuid(),
            groupId ?? Group1Id,
            teacherId ?? Teacher1Id,
            allocationId ?? Allocation1Id,
            subjectName,
            groupLabel,
            null,
            requiredClassroomType,
            maxConsecutiveSlots,
            requiresSpecialist,
            subjectKey,
            teacherSpecialties ?? new List<string> { "Generalista" },
            teacherMaxWeeklyHours);

    public static GenerationContext Context(
        IReadOnlyList<SessionToAssign>? sessions = null,
        IReadOnlyList<IHardConstraint>? hardConstraints = null,
        IReadOnlyList<ISoftConstraint>? softConstraints = null,
        SchoolConfig? school = null,
        int timeoutSeconds = 30)
        => new()
        {
            School = school ?? DefaultSchool(),
            Sessions = sessions ?? [],
            HardConstraints = hardConstraints ?? [],
            SoftConstraints = softConstraints ?? [],
            TimeoutSeconds = timeoutSeconds,
        };
}
