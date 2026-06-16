namespace HorariosEscolares.Application.Features.Teachers;

public record StageAssignmentDto(Guid StageId, string StageName, string StageType, int? Cycle);
public record SubjectHourDto(string SubjectKey, int WeeklyHours);
public record SubjectHourInput(string SubjectKey, int WeeklyHours);
public record TeacherAssignmentDto(Guid Id, Guid GroupId, string GroupDisplay, Guid AllocationId, string SubjectName, int WeeklyHours);

public record TeacherDto(
    Guid Id, string FullName, string Email, string TeacherType,
    int MaxWeeklyHours, SubjectHourDto[] SubjectHours, string ColorKey,
    int AssignedHours, StageAssignmentDto[] StageAssignments,
    TeacherAssignmentDto[] Assignments);
