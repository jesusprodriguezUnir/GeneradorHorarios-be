using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

/// <summary>
/// Detección de conflictos posterior a la generación: huecos de cobertura por grupo
/// y profesores con menos horas asignadas de las configuradas.
/// </summary>
public static class PostProcessConflictDetector
{
    public static List<ConflictExplanation> DetectCoverageConflicts(
        IReadOnlyDictionary<Guid, CourseGroup> allGroups,
        IReadOnlyList<int> workingDays,
        IReadOnlySet<int> lectivoSlotIndices,
        IReadOnlySet<(Guid GroupId, int DayOfWeek, int SlotIndex)> assignedSet)
    {
        var conflicts = new List<ConflictExplanation>();
        foreach (var (groupId, group) in allGroups)
        {
            foreach (var day in workingDays)
            {
                foreach (var slotIdx in lectivoSlotIndices)
                {
                    if (!assignedSet.Contains((groupId, day, slotIdx)))
                    {
                        conflicts.Add(new ConflictExplanation
                        {
                            Type = ConflictType.Coverage, Severity = ConflictSeverity.Warning,
                            Description = $"Hueco sin cubrir para {group.DisplayName} el {DayName(day)} en la hora {slotIdx + 1}.",
                            Suggestions = ["Revisa las asignaciones del grupo o amplía sus horas lectivas."],
                            GroupId = groupId, DayOfWeek = day, SlotIndex = slotIdx,
                        });
                    }
                }
            }
        }
        return conflicts;
    }

    public static List<ConflictExplanation> DetectTeacherHoursConflicts(
        IReadOnlyList<AssignedSlot> assignedSlots,
        IReadOnlyDictionary<Guid, int> teacherExpectedHours,
        IReadOnlyDictionary<Guid, string> teacherNames)
    {
        var teacherActualHours = assignedSlots
            .GroupBy(s => s.TeacherId)
            .ToDictionary(g => g.Key, g => g.Count());

        var conflicts = new List<ConflictExplanation>();
        foreach (var (teacherId, expectedHours) in teacherExpectedHours)
        {
            var actual = teacherActualHours.GetValueOrDefault(teacherId, 0);
            if (actual < expectedHours)
            {
                var name = teacherNames.GetValueOrDefault(teacherId, "Profesor");
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Teacher, Severity = ConflictSeverity.Warning,
                    Description = $"{name} tiene {actual} horas asignadas de {expectedHours} configuradas.",
                    Suggestions = ["Comprueba que no haya conflictos de disponibilidad o aulas especiales sin cubrir."],
                    TeacherId = teacherId,
                });
            }
        }
        return conflicts;
    }

    private static string DayName(int day) => day switch
    {
        1 => "lunes", 2 => "martes", 3 => "miércoles",
        4 => "jueves", 5 => "viernes", 6 => "sábado", _ => "domingo"
    };
}
