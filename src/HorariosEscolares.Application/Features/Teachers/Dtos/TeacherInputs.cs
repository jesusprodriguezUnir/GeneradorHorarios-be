namespace HorariosEscolares.Application.Features.Teachers;

public record StageAssignmentInput(Guid StageId, int? Cycle);
public record TeacherAssignmentInput(Guid AllocationId, Guid GroupId, int WeeklyHours);
