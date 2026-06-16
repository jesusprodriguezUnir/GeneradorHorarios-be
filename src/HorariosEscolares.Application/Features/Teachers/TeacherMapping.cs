using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Application.Features.Teachers;

public static class TeacherMapping
{
    public static TeacherDto ToDto(Teacher teacher, int assignedHours, Dictionary<Guid, SchoolStage> stagesMap,
        TeacherAssignmentDto[]? assignments = null)
    {
        var subjectHours = teacher.SubjectHours
            .Select(sh => new SubjectHourDto(sh.SubjectKey, sh.WeeklyHours))
            .ToArray();

        var stageAssignments = teacher.StageAssignments
            .Select(sa =>
            {
                stagesMap.TryGetValue(sa.StageId, out var stage);
                return new StageAssignmentDto(
                    sa.StageId,
                    stage?.Name ?? "",
                    stage?.StageType ?? "",
                    sa.Cycle);
            })
            .ToArray();

        return new(teacher.Id, teacher.FullName, teacher.Email, teacher.TeacherType,
            teacher.MaxWeeklyHours, subjectHours, teacher.ColorKey, assignedHours, stageAssignments,
            assignments ?? []);
    }
}
