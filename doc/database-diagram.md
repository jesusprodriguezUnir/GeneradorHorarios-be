# Diagrama de Base de Datos — Lectivo

> Generado automáticamente. Última actualización: 2026-06-05.

## Diagrama Entidad-Relación

```mermaid
erDiagram
    School {
        guid Id PK
        string Name
        string Slug UK
        string CenterCode
        string Locality
        string Community
        string Stage
        int MinCourseLevel
        int MaxCourseLevel
        string AcademicYear
        string ScheduleType
        time MorningStart
        time AfternoonStart
        int SlotMinutes
        int BreakAfterSlot
        int BreakMinutes
        int SlotsPerDay
        int AfternoonSlots
        int DaysPerWeek
        string WorkingDays
        datetime CreatedAt
    }

    SchoolStage {
        guid Id PK
        guid SchoolId FK
        string StageType
        string Name
        int MinLevel
        int MaxLevel
        int SortOrder
        string ScheduleType
        time MorningStart
        time AfternoonStart
        int SlotMinutes
        int BreakAfterSlot
        int BreakMinutes
        int SlotsPerDay
        int AfternoonSlots
        int DaysPerWeek
        string WorkingDays
    }

    AppUser {
        guid Id PK
        string Email UK
        string FullName
        guid SchoolId FK
        string Role
        guid TeacherId FK "nullable"
    }

    Teacher {
        guid Id PK
        guid SchoolId FK
        guid UserId FK "nullable"
        string FullName
        string Email
        string TeacherType
        int MaxWeeklyHours
        int MaxDailyConsecutive
        string Specialties "JSON array"
        string ColorKey
    }

    TeacherStageAssignment {
        guid Id PK
        guid TeacherId FK
        guid StageId FK
        int Cycle "nullable"
    }

    Classroom {
        guid Id PK
        guid SchoolId FK
        string Name
        string ClassroomType
        int Capacity
        bool IsShared
    }

    CurriculumTemplate {
        guid Id PK
        guid SchoolId FK "nullable"
        guid StageId FK "nullable"
        string Name
        string Region
        string Stage
        bool IsOfficial
    }

    SubjectAllocation {
        guid Id PK
        guid TemplateId FK
        string SubjectName
        string SubjectShort
        string SubjectKey
        int WeeklyHoursMin
        int WeeklyHoursMax
        int WeeklyHoursDefault
        bool RequiresSpecialist
        string RequiredClassroomType "nullable"
        int MaxConsecutiveSlots
        bool SplittableAcrossDays
        bool IsOfficial
    }

    CourseGroup {
        guid Id PK
        guid SchoolId FK
        guid StageId FK
        int CourseLevel
        string GroupLabel
        int StudentCount
        guid TutorId FK "nullable"
        guid HomeClassroomId FK "nullable"
    }

    GroupSubjectHour {
        guid Id PK
        guid GroupId FK
        string SubjectKey
        int Hours
    }

    Assignment {
        guid Id PK
        guid SchoolId FK
        guid TeacherId FK
        guid GroupId FK
        guid AllocationId FK
        int WeeklyHours
    }

    TeacherConstraint {
        guid Id PK
        guid SchoolId FK
        guid TeacherId FK
        string ConstraintType
        int DayOfWeek
        int SlotIndex
        int Weight
        string Reason "nullable"
    }

    ScheduleRecord {
        guid Id PK
        guid SchoolId FK
        guid StageId FK
        string AcademicYear
        string Status
        datetime GeneratedAt "nullable"
        datetime PublishedAt "nullable"
        int GenerationSeconds "nullable"
        int TotalConflicts
        guid PeriodId FK "nullable"
        guid CreatedBy FK
        datetime CreatedAt
    }

    ScheduleEntry {
        guid Id PK
        guid ScheduleId FK
        guid SchoolId FK
        guid GroupId FK
        guid AllocationId FK
        guid TeacherId FK
        guid ClassroomId FK
        int DayOfWeek
        int SlotIndex
        bool IsManualOverride
    }

    ScheduleConflictRecord {
        guid Id PK
        guid ScheduleId FK
        string ConflictType
        string Severity
        string Description
        string Suggestions "JSON array"
        guid GroupId FK "nullable"
        guid TeacherId FK "nullable"
        int DayOfWeek "nullable"
        int SlotIndex "nullable"
    }

    SchoolPeriod {
        guid Id PK
        guid SchoolId FK
        guid StageId FK
        string Key
        string Name
        string Months "JSON array"
        string ScheduleType
        int SlotMinutes
        int SlotsPerDay
        int AfternoonSlots
        bool IsDefault
        int SortOrder
    }

    CycleSchedule {
        guid Id PK
        guid SchoolId FK
        guid StageId FK
        guid PeriodId FK
        int Cycle
        time MorningStart
        time EndTime
        time AfternoonStart "nullable"
    }

    CycleBreak {
        guid Id PK
        guid CycleScheduleId FK
        int AfterSlot
        int Minutes
    }

    PeriodAssignmentHours {
        guid Id PK
        guid PeriodId FK
        guid AssignmentId FK
        int WeeklyHours
    }

    %% ── Relaciones ──────────────────────────────────────────

    School ||--o{ SchoolStage : "tiene etapas"
    School ||--o{ AppUser : "tiene usuarios"
    School ||--o{ Teacher : "tiene profesores"
    School ||--o{ Classroom : "tiene aulas"
    School ||--o{ CourseGroup : "tiene grupos"
    School ||--o{ Assignment : "tiene asignaciones"
    School ||--o{ TeacherConstraint : "tiene restricciones"
    School ||--o{ ScheduleRecord : "tiene horarios"
    School ||--o{ ScheduleEntry : "tiene entradas"
    School ||--o{ SchoolPeriod : "tiene periodos"
    School ||--o{ CycleSchedule : "tiene ciclos"

    SchoolStage ||--o{ CourseGroup : "agrupa cursos"
    SchoolStage ||--o{ CurriculumTemplate : "define currículo"
    SchoolStage ||--o{ ScheduleRecord : "genera horarios"
    SchoolStage ||--o{ SchoolPeriod : "tiene periodos"
    SchoolStage ||--o{ CycleSchedule : "tiene ciclos"
    SchoolStage ||--o{ TeacherStageAssignment : "asignada a profesores"

    Teacher ||--o{ TeacherStageAssignment : "enseña en"
    Teacher ||--o{ Assignment : "tiene asignaciones"
    Teacher ||--o{ TeacherConstraint : "tiene restricciones"
    Teacher ||--o{ ScheduleEntry : "aparece en"
    Teacher ||--o| AppUser : "tiene usuario"
    Teacher ||--o{ CourseGroup : "es tutor de"

    AppUser }o--|| School : "pertenece a"
    AppUser }o--o| Teacher : "vinculado a"

    CurriculumTemplate ||--o{ SubjectAllocation : "define asignaturas"

    CourseGroup ||--o{ GroupSubjectHour : "horas por asignatura"
    CourseGroup ||--o{ Assignment : "tiene asignaciones"
    CourseGroup ||--o{ ScheduleEntry : "aparece en"
    CourseGroup }o--o| Teacher : "tutor"
    CourseGroup }o--o| Classroom : "aula habitual"

    Assignment }o--|| SubjectAllocation : "de asignatura"
    Assignment ||--o{ PeriodAssignmentHours : "horas por periodo"

    ScheduleRecord ||--o{ ScheduleEntry : "contiene"
    ScheduleRecord ||--o{ ScheduleConflictRecord : "tiene conflictos"

    SchoolPeriod ||--o{ CycleSchedule : "tiene ciclos"
    SchoolPeriod ||--o{ PeriodAssignmentHours : "ajusta horas"

    CycleSchedule ||--o{ CycleBreak : "tiene recreos"

    Classroom ||--o{ ScheduleEntry : "usada en"
```

## Índices únicos

| Tabla | Columnas |
|-------|----------|
| `School` | `Slug` |
| `AppUser` | `Email` |
| `Teacher` | `(SchoolId, Email)` |
| `SchoolStage` | `(SchoolId, StageType)` |
| `CourseGroup` | `(StageId, CourseLevel, GroupLabel)` |
| `GroupSubjectHour` | `(GroupId, SubjectKey)` |
| `Assignment` | `(TeacherId, GroupId, AllocationId)` |
| `CycleSchedule` | `(PeriodId, Cycle)` |
| `SchoolPeriod` | `(StageId, Key)` |
| `PeriodAssignmentHours` | `(PeriodId, AssignmentId)` |
| `TeacherStageAssignment` | `(TeacherId, StageId, Cycle)` |

## Notas

- **Campos JSON como string**: `WorkingDays`, `Specialties`, `Suggestions`, `Months` se almacenan como `nvarchar` con contenido JSON.
- **Ciclos educativos**: se resuelven dinámicamente por `CycleResolver` a partir del nivel del curso (`CourseLevel`) y el tipo de etapa (`StageType`).
- **Tablas con nombre personalizado**: `ScheduleRecord` → tabla `Schedules`, `ScheduleConflictRecord` → tabla `ScheduleConflicts`.
- **BD**: SQL Server (contenedor Docker en desarrollo).
