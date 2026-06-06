namespace HorariosEscolares.Domain.Entities;

public class School
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? CenterCode { get; set; }
    public string? Locality { get; set; }
    public string Community { get; set; } = "madrid";
    public int MinCourseLevel { get; set; } = 1;
    public int MaxCourseLevel { get; set; } = 6;
    public string AcademicYear { get; set; } = "2025/2026";
    public string ScheduleType { get; set; } = "continua";
    public TimeOnly MorningStart { get; set; } = new(9, 0);
    public TimeOnly? AfternoonStart { get; set; }
    public int SlotMinutes { get; set; } = 60;
    public int BreakAfterSlot { get; set; } = 2;
    public int BreakMinutes { get; set; } = 30;
    public int SlotsPerDay { get; set; } = 5;
    public int AfternoonSlots { get; set; } = 0;
    public int DaysPerWeek { get; set; } = 5;
    public string WorkingDays { get; set; } = "[1,2,3,4,5]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required Guid SchoolId { get; set; }
    public required Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid? TeacherId { get; set; }
}

public enum RoleKind { Admin, Teacher, Other }

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required RoleKind Kind { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; } = true;
}

public static class RoleIds
{
    public static readonly Guid Director         = Guid.Parse("00000000-0000-0000-0000-0000000000A1");
    public static readonly Guid JefeEstudios     = Guid.Parse("00000000-0000-0000-0000-0000000000A2");
    public static readonly Guid Secretario       = Guid.Parse("00000000-0000-0000-0000-0000000000A3");
    public static readonly Guid Profesor         = Guid.Parse("00000000-0000-0000-0000-0000000000B1");
    public static readonly Guid Tutor            = Guid.Parse("00000000-0000-0000-0000-0000000000B2");
    public static readonly Guid CoordinadorCiclo = Guid.Parse("00000000-0000-0000-0000-0000000000B3");
    public static readonly Guid Orientador       = Guid.Parse("00000000-0000-0000-0000-0000000000C1");
}

public static class RoleCodes
{
    public const string Director         = "director";
    public const string JefeEstudios     = "jefe_estudios";
    public const string Secretario       = "secretario";
    public const string Profesor         = "profesor";
    public const string Tutor            = "tutor";
    public const string CoordinadorCiclo = "coordinador_ciclo";
    public const string Orientador       = "orientador";
}

public class Teacher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public Guid? UserId { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string TeacherType { get; set; } = "definitivo";
    public int MaxWeeklyHours { get; set; } = 25;
    public int MaxDailyConsecutive { get; set; } = 4;
    public string Specialties { get; set; } = "[]";
    public string ColorKey { get; set; } = "mat";
    public ICollection<TeacherStageAssignment> StageAssignments { get; set; } = new List<TeacherStageAssignment>();
}

/// <summary>
/// Asociación de un profesor con una etapa educativa y (opcionalmente) un ciclo concreto.
/// Si <see cref="Cycle"/> es null, el profesor puede dar clase en cualquier ciclo de la etapa.
/// </summary>
public class TeacherStageAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid TeacherId { get; set; }
    public required Guid StageId { get; set; }
    /// <summary>Ciclo educativo (1, 2, 3…). Null = toda la etapa.</summary>
    public int? Cycle { get; set; }
    public Teacher? Teacher { get; set; }
    public SchoolStage? Stage { get; set; }
}

public class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required string Name { get; set; }
    public string ClassroomType { get; set; } = "regular";
    public int Capacity { get; set; } = 30;
    public bool IsShared { get; set; } = false;
}

public class CurriculumTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SchoolId { get; set; }
    public Guid? StageId { get; set; }
    public required string Name { get; set; }
    public string Region { get; set; } = "madrid";
    public string Stage { get; set; } = "primaria";
    public bool IsOfficial { get; set; } = false;
}

public class SubjectAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid TemplateId { get; set; }
    public required string SubjectName { get; set; }
    public string SubjectShort { get; set; } = "";
    public string SubjectKey { get; set; } = "tut";
    public int WeeklyHoursMin { get; set; }
    public int WeeklyHoursMax { get; set; }
    public int WeeklyHoursDefault { get; set; }
    public bool RequiresSpecialist { get; set; } = false;
    public string? RequiredClassroomType { get; set; }
    public int MaxConsecutiveSlots { get; set; } = 2;
    public bool SplittableAcrossDays { get; set; } = true;
    public bool IsOfficial { get; set; } = true;
}

public class CourseGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid StageId { get; set; }
    public required int CourseLevel { get; set; }
    public required string GroupLabel { get; set; }
    public int StudentCount { get; set; } = 25;
    public Guid? TutorId { get; set; }
    public Guid? HomeClassroomId { get; set; }
    public ICollection<GroupSubjectHour> SubjectHoursList { get; set; } = new List<GroupSubjectHour>();
    public string DisplayName => $"{CourseLevel}º{GroupLabel}";
}

public class GroupSubjectHour
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid GroupId { get; set; }
    public required string SubjectKey { get; set; }
    public int Hours { get; set; }
    public CourseGroup? Group { get; set; }
}

public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid TeacherId { get; set; }
    public required Guid GroupId { get; set; }
    public required Guid AllocationId { get; set; }
    public int WeeklyHours { get; set; }
}

public class TeacherConstraint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid TeacherId { get; set; }
    public required string ConstraintType { get; set; }
    public required int DayOfWeek { get; set; }
    public required int SlotIndex { get; set; }
    public int Weight { get; set; } = 10;
    public string? Reason { get; set; }
}

public class ScheduleRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid StageId { get; set; }
    public required string AcademicYear { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? GeneratedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? GenerationSeconds { get; set; }
    public int TotalConflicts { get; set; } = 0;
    public Guid? PeriodId { get; set; }
    public required Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ScheduleEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid ScheduleId { get; set; }
    public required Guid SchoolId { get; set; }
    public required Guid GroupId { get; set; }
    public required Guid AllocationId { get; set; }
    public required Guid TeacherId { get; set; }
    public required Guid ClassroomId { get; set; }
    public required int DayOfWeek { get; set; }
    public required int SlotIndex { get; set; }
    public bool IsManualOverride { get; set; } = false;
}

public class ScheduleConflictRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid ScheduleId { get; set; }
    public required string ConflictType { get; set; }
    public required string Severity { get; set; }
    public required string Description { get; set; }
    public string Suggestions { get; set; } = "[]";
    public Guid? GroupId { get; set; }
    public Guid? TeacherId { get; set; }
    public int? DayOfWeek { get; set; }
    public int? SlotIndex { get; set; }
}

public class SchoolPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid StageId { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string Months { get; set; } = "[10,11,12,1,2,3,4,5]";
    public string ScheduleType { get; set; } = "partida";
    public int SlotMinutes { get; set; } = 60;
    public int SlotsPerDay { get; set; } = 5;
    public int AfternoonSlots { get; set; } = 0;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public ICollection<CycleSchedule> Cycles { get; set; } = new List<CycleSchedule>();
}

public class CycleSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid StageId { get; set; }
    public required Guid PeriodId { get; set; }
    public required int Cycle { get; set; }
    public TimeOnly MorningStart { get; set; } = new(9, 0);
    public TimeOnly EndTime { get; set; } = new(14, 0);
    public TimeOnly? AfternoonStart { get; set; }
    public SchoolPeriod? Period { get; set; }
    public ICollection<CycleBreak> Breaks { get; set; } = new List<CycleBreak>();
}

public class PeriodAssignmentHours
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid PeriodId { get; set; }
    public required Guid AssignmentId { get; set; }
    public required int WeeklyHours { get; set; }
}

public class CycleBreak
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid CycleScheduleId { get; set; }
    public required int AfterSlot { get; set; }
    public int Minutes { get; set; } = 30;
    public CycleSchedule? Cycle { get; set; }
}

/// <summary>
/// Etapa educativa ("bloque") de un colegio: Infantil, Primaria o Secundaria.
/// Un colegio puede impartir varias etapas, cada una con sus cursos, ciclos y jornada propios.
/// </summary>
public class SchoolStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required string StageType { get; set; }   // StageTypes.Infantil | Primaria | Secundaria
    public required string Name { get; set; }
    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 6;
    public int SortOrder { get; set; }

    // Configuración de jornada propia de la etapa
    public string ScheduleType { get; set; } = "continua";
    public TimeOnly MorningStart { get; set; } = new(9, 0);
    public TimeOnly? AfternoonStart { get; set; }
    public int SlotMinutes { get; set; } = 60;
    public int BreakAfterSlot { get; set; } = 2;
    public int BreakMinutes { get; set; } = 30;
    public int SlotsPerDay { get; set; } = 5;
    public int AfternoonSlots { get; set; } = 0;
    public int DaysPerWeek { get; set; } = 5;
    public string WorkingDays { get; set; } = "[1,2,3,4,5]";
}

/// <summary>Tipos de etapa educativa soportados.</summary>
public static class StageTypes
{
    public const string Infantil = "infantil";
    public const string Primaria = "primaria";
    public const string Secundaria = "secundaria";
}
