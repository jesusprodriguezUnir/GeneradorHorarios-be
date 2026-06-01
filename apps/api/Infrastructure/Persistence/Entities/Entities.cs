namespace HorariosEscolares.Infrastructure.Persistence.Entities;

// ── School ───────────────────────────────────────────────────────────────────
public class School
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }

    // ── Identificación del centro ─────────────────────────────────────────────
    public string? CenterCode { get; set; }          // Código oficial del centro (ej. CAM)
    public string? Locality { get; set; }            // Localidad/municipio
    public string Community { get; set; } = "madrid";
    public string Stage { get; set; } = "primaria";
    public int MinCourseLevel { get; set; } = 1;
    public int MaxCourseLevel { get; set; } = 6;
    public string AcademicYear { get; set; } = "2025/2026";

    // ── Configuración de jornada ──────────────────────────────────────────────
    public string ScheduleType { get; set; } = "continua";
    public TimeOnly MorningStart { get; set; } = new(9, 0);
    public TimeOnly? AfternoonStart { get; set; }
    public int SlotMinutes { get; set; } = 60;
    public int BreakAfterSlot { get; set; } = 2;
    public int BreakMinutes { get; set; } = 30;
    public int SlotsPerDay { get; set; } = 5;

    /// <summary>Nº de slots de la jornada de tarde (solo jornada partida). 0 = jornada continua.</summary>
    public int AfternoonSlots { get; set; } = 0;

    /// <summary>Derivado de WorkingDays.Count; se sincroniza al guardar.</summary>
    public int DaysPerWeek { get; set; } = 5;

    /// <summary>Días lectivos de la semana como JSON de ints (1=lunes…7=domingo). Ej: "[1,2,3,4,5]".</summary>
    public string WorkingDays { get; set; } = "[1,2,3,4,5]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── AppUser (simulado hasta integrar Supabase/AD) ─────────────────────────────
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required Guid SchoolId { get; set; }
    public required string Role { get; set; }   // school_admin | teacher
    public Guid? TeacherId { get; set; }        // null para admin
}

// ── Teacher ──────────────────────────────────────────────────────────────────
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
    public string Specialties { get; set; } = "[]";  // JSON array
    public string ColorKey { get; set; } = "mat";
}

// ── Classroom ─────────────────────────────────────────────────────────────────
public class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required string Name { get; set; }
    public string ClassroomType { get; set; } = "regular";
    public int Capacity { get; set; } = 30;
    public bool IsShared { get; set; } = false;
}

// ── CurriculumTemplate ────────────────────────────────────────────────────────
public class CurriculumTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SchoolId { get; set; }
    public required string Name { get; set; }
    public string Region { get; set; } = "madrid";
    public string Stage { get; set; } = "primaria";
    public bool IsOfficial { get; set; } = false;
}

// ── SubjectAllocation ─────────────────────────────────────────────────────────
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

// ── CourseGroup ───────────────────────────────────────────────────────────────
public class CourseGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required int CourseLevel { get; set; }
    public required string GroupLabel { get; set; }
    public int StudentCount { get; set; } = 25;
    public Guid? TutorId { get; set; }
    public Guid? HomeClassroomId { get; set; }
    public string SubjectHours { get; set; } = "{}";  // JSON dictionary SubjectKey -> Hours

    // computed — ignorada por EF
    public string DisplayName => $"{CourseLevel}º{GroupLabel}";
}

// ── Assignment ────────────────────────────────────────────────────────────────
public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required Guid TeacherId { get; set; }
    public required Guid GroupId { get; set; }
    public required Guid AllocationId { get; set; }
    public int WeeklyHours { get; set; }
}

// ── TeacherConstraint ─────────────────────────────────────────────────────────
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

// ── ScheduleRecord (prefijado para no colisionar con Domain.Entities.Schedule) ─
public class ScheduleRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }
    public required string AcademicYear { get; set; }
    public string Status { get; set; } = "draft";   // draft|generated|published|archived
    public DateTime? GeneratedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? GenerationSeconds { get; set; }
    public int TotalConflicts { get; set; } = 0;
    public required Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── ScheduleEntry ─────────────────────────────────────────────────────────────
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

// ── CycleSchedule ─────────────────────────────────────────────────────────────
/// <summary>
/// Configuración de jornada (entrada/salida) para un ciclo educativo del colegio.
/// Ciclo 1 = 1º-2º, Ciclo 2 = 3º-4º, Ciclo 3 = 5º-6º.
/// Los parámetros globales de jornada (SlotMinutes, BreakAfterSlot, SlotsPerDay…)
/// siguen siendo comunes a todo el colegio en la entidad School.
/// </summary>
public class CycleSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SchoolId { get; set; }

    /// <summary>Número de ciclo: 1, 2 o 3.</summary>
    public required int Cycle { get; set; }

    /// <summary>Hora de entrada de mañana para este ciclo.</summary>
    public TimeOnly MorningStart { get; set; } = new(9, 0);

    /// <summary>Hora de salida (fin de jornada) para este ciclo.</summary>
    public TimeOnly EndTime { get; set; } = new(14, 0);

    /// <summary>Hora de inicio de tarde (solo jornada partida).</summary>
    public TimeOnly? AfternoonStart { get; set; }
}

// ── ScheduleConflictRecord ────────────────────────────────────────────────────
public class ScheduleConflictRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid ScheduleId { get; set; }
    public required string ConflictType { get; set; }
    public required string Severity { get; set; }
    public required string Description { get; set; }
    public string Suggestions { get; set; } = "[]";  // JSON array serializado
    public Guid? GroupId { get; set; }
    public Guid? TeacherId { get; set; }
    public int? DayOfWeek { get; set; }
    public int? SlotIndex { get; set; }
}
