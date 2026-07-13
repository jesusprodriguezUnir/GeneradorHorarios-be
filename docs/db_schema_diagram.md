# Estructura de la Base de Datos - Lectivo

Este documento describe el esquema de la base de datos de **Lectivo**, la aplicación para la generación automática de horarios escolares.
Este archivo se genera automáticamente analizando las entidades de persistencia C# mediante la Skill `db-diagram-generator`.

## Diagrama Entidad-Relación (Mermaid)

```mermaid
erDiagram
    School {
        GUID Id PK
        String Name 
        String Slug 
        String (nullable) CenterCode 
        String (nullable) Locality 
        String Community 
        String Stage 
        Integer MinCourseLevel 
        Integer MaxCourseLevel 
        String AcademicYear 
        String ScheduleType 
        TimeOnly MorningStart 
        TimeOnly (nullable) AfternoonStart 
        Integer SlotMinutes 
        Integer BreakAfterSlot 
        Integer BreakMinutes 
        Integer SlotsPerDay 
        Integer AfternoonSlots 
        Integer DaysPerWeek 
        String WorkingDays 
        DateTime CreatedAt 
    }

    AppUser {
        GUID Id PK
        String Email 
        String FullName 
        GUID SchoolId FK
        String Role 
        GUID (nullable) TeacherId FK
    }

    Teacher {
        GUID Id PK
        GUID SchoolId FK
        GUID (nullable) UserId FK
        String FullName 
        String Email 
        String TeacherType 
        Integer MaxWeeklyHours 
        Integer MaxDailyConsecutive 
        String Specialties 
        String ColorKey 
    }

    TeacherStageAssignment {
        GUID Id PK
        GUID TeacherId FK
        GUID StageId FK
        Integer (nullable) Cycle 
        Teacher? Teacher 
        SchoolStage? Stage 
    }

    Classroom {
        GUID Id PK
        GUID SchoolId FK
        String Name 
        String ClassroomType 
        Integer Capacity 
        Boolean IsShared 
    }

    CurriculumTemplate {
        GUID Id PK
        GUID (nullable) SchoolId FK
        GUID (nullable) StageId FK
        String Name 
        String Region 
        String Stage 
        Boolean IsOfficial 
    }

    SubjectAllocation {
        GUID Id PK
        GUID TemplateId FK
        String SubjectName 
        String SubjectShort 
        String SubjectKey 
        Integer WeeklyHoursMin 
        Integer WeeklyHoursMax 
        Integer WeeklyHoursDefault 
        Boolean RequiresSpecialist 
        String (nullable) RequiredClassroomType 
        Integer MaxConsecutiveSlots 
        Boolean SplittableAcrossDays 
        Boolean IsOfficial 
    }

    CourseGroup {
        GUID Id PK
        GUID SchoolId FK
        GUID StageId FK
        Integer CourseLevel 
        String GroupLabel 
        Integer StudentCount 
        GUID (nullable) TutorId FK
        GUID (nullable) HomeClassroomId FK
    }

    GroupSubjectHour {
        GUID Id PK
        GUID GroupId FK
        String SubjectKey 
        Integer Hours 
        CourseGroup? Group 
    }

    Assignment {
        GUID Id PK
        GUID SchoolId FK
        GUID TeacherId FK
        GUID GroupId FK
        GUID AllocationId FK
        Integer WeeklyHours 
    }

    TeacherConstraint {
        GUID Id PK
        GUID SchoolId FK
        GUID TeacherId FK
        String ConstraintType 
        Integer DayOfWeek 
        Integer SlotIndex 
        Integer Weight 
        String (nullable) Reason 
    }

    ScheduleRecord {
        GUID Id PK
        GUID SchoolId FK
        GUID StageId FK
        String AcademicYear 
        String Status 
        DateTime (nullable) GeneratedAt 
        DateTime (nullable) PublishedAt 
        Integer (nullable) GenerationSeconds 
        Integer TotalConflicts 
        GUID (nullable) PeriodId FK
        GUID CreatedBy 
        DateTime CreatedAt 
    }

    ScheduleEntry {
        GUID Id PK
        GUID ScheduleId FK
        GUID SchoolId FK
        GUID GroupId FK
        GUID AllocationId FK
        GUID TeacherId FK
        GUID ClassroomId FK
        Integer DayOfWeek 
        Integer SlotIndex 
        Boolean IsManualOverride 
    }

    ScheduleConflictRecord {
        GUID Id PK
        GUID ScheduleId FK
        String ConflictType 
        String Severity 
        String Description 
        String Suggestions 
        GUID (nullable) GroupId FK
        GUID (nullable) TeacherId FK
        Integer (nullable) DayOfWeek 
        Integer (nullable) SlotIndex 
    }

    SchoolPeriod {
        GUID Id PK
        GUID SchoolId FK
        GUID StageId FK
        String Key 
        String Name 
        String Months 
        String ScheduleType 
        Integer SlotMinutes 
        Integer SlotsPerDay 
        Integer AfternoonSlots 
        Boolean IsDefault 
        Integer SortOrder 
    }

    CycleSchedule {
        GUID Id PK
        GUID SchoolId FK
        GUID StageId FK
        GUID PeriodId FK
        Integer Cycle 
        TimeOnly MorningStart 
        TimeOnly EndTime 
        TimeOnly (nullable) AfternoonStart 
        SchoolPeriod? Period 
    }

    PeriodAssignmentHours {
        GUID Id PK
        GUID PeriodId FK
        GUID AssignmentId FK
        Integer WeeklyHours 
    }

    CycleBreak {
        GUID Id PK
        GUID CycleScheduleId FK
        Integer AfterSlot 
        Integer Minutes 
        CycleSchedule? Cycle 
    }

    SchoolStage {
        GUID Id PK
        GUID SchoolId FK
        String StageType 
        String Name 
        Integer MinLevel 
        Integer MaxLevel 
        Integer SortOrder 
        String ScheduleType 
        TimeOnly MorningStart 
        TimeOnly (nullable) AfternoonStart 
        Integer SlotMinutes 
        Integer BreakAfterSlot 
        Integer BreakMinutes 
        Integer SlotsPerDay 
        Integer AfternoonSlots 
        Integer DaysPerWeek 
        String WorkingDays 
    }

    School ||--o{ SchoolStage : "tiene"
    SchoolStage ||--o{ CourseGroup : "tiene"
    SchoolStage ||--o{ TeacherStageAssignment : "filtra profesores"
    School ||--o{ Teacher : "tiene"
    Teacher ||--o{ TeacherStageAssignment : "etapas asignadas"
    School ||--o{ Classroom : "tiene"
    SchoolStage ||--o{ SchoolPeriod : "tiene"
    SchoolPeriod ||--o{ CycleSchedule : "ciclos del periodo"
    CycleSchedule ||--o{ CycleBreak : "recreos del ciclo"
    CurriculumTemplate ||--o{ SubjectAllocation : "materias configuradas"
    SchoolStage ||--o{ CurriculumTemplate : "plantilla curricular"
    CourseGroup ||--o{ GroupSubjectHour : "horas por materia"
    CourseGroup |o--o| Teacher : "tutor"
    CourseGroup |o--o| Classroom : "aula habitual"
    Assignment ||--o{ PeriodAssignmentHours : "horas por periodo"
    SchoolPeriod ||--o{ PeriodAssignmentHours : "periodo de la asignacion"
    Teacher ||--o{ Assignment : "tiene asignaciones"
    CourseGroup ||--o{ Assignment : "tiene asignaciones"
    SubjectAllocation ||--o{ Assignment : "tiene asignaciones"
    School ||--o{ Assignment : "tiene asignaciones"
    Teacher ||--o{ TeacherConstraint : "restricciones de horario"
    School ||--o{ TeacherConstraint : "restricciones de horario"
    ScheduleRecord ||--o{ ScheduleEntry : "entradas del horario"
    ScheduleRecord ||--o{ ScheduleConflictRecord : "conflictos del horario"
    ScheduleRecord } |--|| SchoolPeriod : "periodo del horario"
    ScheduleEntry } |--|| CourseGroup : "grupo"
    ScheduleEntry } |--|| SubjectAllocation : "materia"
    ScheduleEntry } |--|| Teacher : "profesor"
    ScheduleEntry } |--|| Classroom : "aula"
```

## Guía y Explicación de las Tablas (Español)

A continuación se presenta una explicación detallada del propósito de cada tabla y sus campos clave:

### Tabla: `School`
**Propósito:** Configuración general y datos básicos de un centro escolar (colegio), como nombre, código, año académico, jornada general y días laborables.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `Name` | `String` | **** | Nombre descriptivo de la entidad. |
| `Slug` | `String` | **** | Identificador amigable en la URL. |
| `CenterCode` | `String (nullable)` | **** | Código oficial del centro escolar asignado por la Consejería de Educación. |
| `Locality` | `String (nullable)` | **** | Localidad / Municipio donde se ubica el colegio. |
| `Community` | `String` | **** | Comunidad Autónoma a la que pertenece el centro (por defecto, 'madrid'). |
| `Stage` | `String` | **** | Objeto de navegación de la etapa escolar (Ignorado en persistencia). |
| `MinCourseLevel` | `Integer` | **** | Curso académico mínimo de la etapa (ej. 1 para Primero de Primaria). |
| `MaxCourseLevel` | `Integer` | **** | Curso académico máximo de la etapa (ej. 6 para Sexto de Primaria). |
| `AcademicYear` | `String` | **** | Curso escolar correspondiente (ej. '2025/2026'). |
| `ScheduleType` | `String` | **** | Tipo de jornada escolar ('continua' o 'partida'). |
| `MorningStart` | `TimeOnly` | **** | Hora de inicio de las clases por la mañana. |
| `AfternoonStart` | `TimeOnly (nullable)` | **** | Hora de inicio de las clases por la tarde (en jornada partida). |
| `SlotMinutes` | `Integer` | **** | Duración de cada período o sesión lectiva (en minutos, ej. 45 o 60). |
| `BreakAfterSlot` | `Integer` | **** | Número de sesión lectiva tras la cual se realiza el descanso/recreo (ej. tras la 2ª sesión). |
| `BreakMinutes` | `Integer` | **** | Duración en minutos del recreo o descanso general. |
| `SlotsPerDay` | `Integer` | **** | Número total de sesiones de clase al día. |
| `AfternoonSlots` | `Integer` | **** | Número de sesiones de clase que se imparten en el horario de tarde. |
| `DaysPerWeek` | `Integer` | **** | Número de días lectivos a la semana (normalmente 5). |
| `WorkingDays` | `String` | **** | Días laborables de la semana representados en formato JSON array (ej. '[1,2,3,4,5]'). |
| `CreatedAt` | `DateTime` | **** | Fecha y hora de creación del registro en el sistema. |

### Tabla: `AppUser`
**Propósito:** Usuarios con acceso a la aplicación, indicando su correo, nombre completo, rol asignado y su vinculación con un profesor físico.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `Email` | `String` | **** | Dirección de correo electrónico (única). |
| `FullName` | `String` | **** | Nombre y apellidos completos. |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `Role` | `String` | **** | Rol del usuario (ej. 'admin', 'teacher'). |
| `TeacherId` | `GUID (nullable)` | **FK** | Clave foránea que vincula con el profesor (Teacher). |

### Tabla: `Teacher`
**Propósito:** Personal docente del colegio. Contiene información sobre sus límites de horas semanales y celdas consecutivas al día, sus especialidades y color asignado.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `UserId` | `GUID (nullable)` | **FK** | Clave foránea que vincula esta fila con la tabla `User`. |
| `FullName` | `String` | **** | Nombre y apellidos completos. |
| `Email` | `String` | **** | Dirección de correo electrónico (única). |
| `TeacherType` | `String` | **** | - |
| `MaxWeeklyHours` | `Integer` | **** | Límite máximo de horas lectivas semanales que puede impartir. |
| `MaxDailyConsecutive` | `Integer` | **** | Límite máximo de sesiones consecutivas de clase que puede dar al día sin descanso. |
| `Specialties` | `String` | **** | Especialidades del docente en formato JSON array (ej. ['ingles', 'musica']). |
| `ColorKey` | `String` | **** | Código de color hexadecimal o abreviatura visual para la interfaz gráfica. |

### Tabla: `TeacherStageAssignment`
**Propósito:** Asociación de un profesor con una etapa educativa (Infantil, Primaria, Secundaria) y opcionalmente con un ciclo concreto, limitando dónde puede dar clase.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `TeacherId` | `GUID` | **FK** | Clave foránea que vincula con el profesor (Teacher). |
| `StageId` | `GUID` | **FK** | Clave foránea que asocia la fila con la etapa escolar (SchoolStage). |
| `Cycle` | `Integer (nullable)` | **** | Ciclo educativo al que pertenece (ej. 1 para primer ciclo, 2 para segundo ciclo, etc.). |
| `Teacher` | `Teacher?` | **** | Objeto de navegación del profesor (Ignorado en persistencia). |
| `Stage` | `SchoolStage?` | **** | Objeto de navegación de la etapa escolar (Ignorado en persistencia). |

### Tabla: `Classroom`
**Propósito:** Espacios físicos y aulas del colegio (aulas ordinarias de grupo, aulas específicas de música, gimnasio, etc.), su capacidad y si es compartida.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `Name` | `String` | **** | Nombre descriptivo de la entidad. |
| `ClassroomType` | `String` | **** | - |
| `Capacity` | `Integer` | **** | Capacidad máxima de alumnos que admite el aula física. |
| `IsShared` | `Boolean` | **** | Indica si el aula puede ser compartida por varios grupos simultáneamente. |

### Tabla: `CurriculumTemplate`
**Propósito:** Plantillas curriculares que definen la estructura general de materias y horas oficiales vigentes para una etapa educativa y región.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID (nullable)` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `StageId` | `GUID (nullable)` | **FK** | Clave foránea que asocia la fila con la etapa escolar (SchoolStage). |
| `Name` | `String` | **** | Nombre descriptivo de la entidad. |
| `Region` | `String` | **** | - |
| `Stage` | `String` | **** | Objeto de navegación de la etapa escolar (Ignorado en persistencia). |
| `IsOfficial` | `Boolean` | **** | - |

### Tabla: `SubjectAllocation`
**Propósito:** Configuración y restricciones específicas para una asignatura de la plantilla curricular (ej. Matemáticas, Lengua), como horas mínimas/máximas y si requiere especialista.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `TemplateId` | `GUID` | **FK** | Clave foránea que vincula con la plantilla curricular (CurriculumTemplate). |
| `SubjectName` | `String` | **** | Nombre completo de la asignatura (ej. 'Matemáticas'). |
| `SubjectShort` | `String` | **** | Nombre abreviado o siglas de la asignatura (ej. 'MAT'). |
| `SubjectKey` | `String` | **** | Código corto identificador único de la asignatura (ej. 'mat', 'len'). |
| `WeeklyHoursMin` | `Integer` | **** | Horas mínimas recomendadas a la semana para esta materia. |
| `WeeklyHoursMax` | `Integer` | **** | Horas máximas permitidas a la semana para esta materia. |
| `WeeklyHoursDefault` | `Integer` | **** | Horas semanales por defecto indicadas para la materia. |
| `RequiresSpecialist` | `Boolean` | **** | Indica si la materia debe ser impartida obligatoriamente por un profesor especialista. |
| `RequiredClassroomType` | `String (nullable)` | **** | Tipo de aula requerido si es específico (ej. 'musica', 'gimnasio'). |
| `MaxConsecutiveSlots` | `Integer` | **** | Número máximo de sesiones consecutivas permitidas para esta materia en un mismo día (ej. 2). |
| `SplittableAcrossDays` | `Boolean` | **** | Indica si las horas semanales de esta materia se pueden repartir en diferentes días. |
| `IsOfficial` | `Boolean` | **** | - |

### Tabla: `CourseGroup`
**Propósito:** Grupos de alumnos de un nivel y letra concretos (ej. 1º A, 3º B), asociados a un tutor de referencia y a su aula principal.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `StageId` | `GUID` | **FK** | Clave foránea que asocia la fila con la etapa escolar (SchoolStage). |
| `CourseLevel` | `Integer` | **** | Nivel educativo del grupo (ej. 1 para 1º, 6 para 6º). |
| `GroupLabel` | `String` | **** | Letra identificadora del grupo (ej. 'A', 'B'). |
| `StudentCount` | `Integer` | **** | Número total de alumnos matriculados en el grupo. |
| `TutorId` | `GUID (nullable)` | **FK** | Clave foránea del profesor que ejerce la tutoría del grupo. |
| `HomeClassroomId` | `GUID (nullable)` | **FK** | Clave foránea del aula asignada por defecto a este grupo (aula de referencia). |

### Tabla: `GroupSubjectHour`
**Propósito:** Especificación de las horas lectivas que un grupo en concreto debe recibir de una asignatura determinada.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `GroupId` | `GUID` | **FK** | Clave foránea que vincula con el grupo (CourseGroup). |
| `SubjectKey` | `String` | **** | Código corto identificador único de la asignatura (ej. 'mat', 'len'). |
| `Hours` | `Integer` | **** | Horas semanales de la materia para el grupo. |
| `Group` | `CourseGroup?` | **** | Objeto de navegación del grupo al que pertenece (Ignorado en persistencia). |

### Tabla: `Assignment`
**Propósito:** Asignación de docencia. Vincula a un profesor con un grupo de alumnos y una asignatura para impartir un número de horas semanales.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `TeacherId` | `GUID` | **FK** | Clave foránea que vincula con el profesor (Teacher). |
| `GroupId` | `GUID` | **FK** | Clave foránea que vincula con el grupo (CourseGroup). |
| `AllocationId` | `GUID` | **FK** | Clave foránea que vincula con la asignatura/materia (SubjectAllocation). |
| `WeeklyHours` | `Integer` | **** | Total de horas semanales que se imparten en la asignación. |

### Tabla: `TeacherConstraint`
**Propósito:** Restricciones horarias de los profesores (ej. no dar clase los viernes por la tarde, mañanas libres por reducciones, etc.).

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `TeacherId` | `GUID` | **FK** | Clave foránea que vincula con el profesor (Teacher). |
| `ConstraintType` | `String` | **** | Tipo de restricción de disponibilidad (ej. 'unavailable', 'preferred'). |
| `DayOfWeek` | `Integer` | **** | Día de la semana (1 = Lunes, 5 = Viernes). |
| `SlotIndex` | `Integer` | **** | Índice del periodo horario en el día (0 = primera sesión). |
| `Weight` | `Integer` | **** | - |
| `Reason` | `String (nullable)` | **** | - |

### Tabla: `ScheduleRecord`
**Propósito:** Cabecera del horario de una etapa escolar. Registra el estado (borrador, publicado), la fecha de generación y estadísticas generales.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `StageId` | `GUID` | **FK** | Clave foránea que asocia la fila con la etapa escolar (SchoolStage). |
| `AcademicYear` | `String` | **** | Curso escolar correspondiente (ej. '2025/2026'). |
| `Status` | `String` | **** | Estado actual del horario (ej. 'draft', 'published'). |
| `GeneratedAt` | `DateTime (nullable)` | **** | Fecha y hora en que se completó la generación del horario. |
| `PublishedAt` | `DateTime (nullable)` | **** | Fecha y hora de publicación oficial del horario. |
| `GenerationSeconds` | `Integer (nullable)` | **** | Tiempo total (en segundos) empleado por el motor de backtracking en resolver el horario. |
| `TotalConflicts` | `Integer` | **** | Número total de conflictos (errores duros) presentes en este horario. |
| `PeriodId` | `GUID (nullable)` | **FK** | Clave foránea que vincula con el periodo del calendario (SchoolPeriod). |
| `CreatedBy` | `GUID` | **** | Clave foránea del usuario que creó e inició la generación del horario. |
| `CreatedAt` | `DateTime` | **** | Fecha y hora de creación del registro en el sistema. |

### Tabla: `ScheduleEntry`
**Propósito:** Cada una de las sesiones de clase asignadas en la cuadrícula horaria. Conecta un día y hora con profesor, grupo, asignatura y aula.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `ScheduleId` | `GUID` | **FK** | Clave foránea que vincula esta fila con la tabla `Schedule`. |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `GroupId` | `GUID` | **FK** | Clave foránea que vincula con el grupo (CourseGroup). |
| `AllocationId` | `GUID` | **FK** | Clave foránea que vincula con la asignatura/materia (SubjectAllocation). |
| `TeacherId` | `GUID` | **FK** | Clave foránea que vincula con el profesor (Teacher). |
| `ClassroomId` | `GUID` | **FK** | Clave foránea que vincula con el aula (Classroom). |
| `DayOfWeek` | `Integer` | **** | Día de la semana (1 = Lunes, 5 = Viernes). |
| `SlotIndex` | `Integer` | **** | Índice del periodo horario en el día (0 = primera sesión). |
| `IsManualOverride` | `Boolean` | **** | Indica si esta celda del horario se ha fijado manualmente y no debe ser alterada por el motor de generación. |

### Tabla: `ScheduleConflictRecord`
**Propósito:** Conflictos y advertencias detectados por el motor de horarios al validar el horario actual (ej. colisiones de profesores o aulas).

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `ScheduleId` | `GUID` | **FK** | Clave foránea que vincula esta fila con la tabla `Schedule`. |
| `ConflictType` | `String` | **** | Tipo de conflicto detectado (ej. 'Teacher', 'Classroom', 'Normative'). |
| `Severity` | `String` | **** | Severidad del problema ('Error' para restricciones duras o 'Warning' para blandas). |
| `Description` | `String` | **** | Descripción detallada del conflicto encontrado. |
| `Suggestions` | `String` | **** | Lista de sugerencias de solución propuestas en formato JSON array. |
| `GroupId` | `GUID (nullable)` | **FK** | Clave foránea que vincula con el grupo (CourseGroup). |
| `TeacherId` | `GUID (nullable)` | **FK** | Clave foránea que vincula con el profesor (Teacher). |
| `DayOfWeek` | `Integer (nullable)` | **** | Día de la semana (1 = Lunes, 5 = Viernes). |
| `SlotIndex` | `Integer (nullable)` | **** | Índice del periodo horario en el día (0 = primera sesión). |

### Tabla: `SchoolPeriod`
**Propósito:** Periodos lectivos específicos del año (ej. Jornada continua en Septiembre/Junio vs Jornada partida el resto del año).

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `StageId` | `GUID` | **FK** | Clave foránea que asocia la fila con la etapa escolar (SchoolStage). |
| `Key` | `String` | **** | Código identificativo del periodo escolar (ej. 'ordinario', 'septiembre_junio'). |
| `Name` | `String` | **** | Nombre descriptivo de la entidad. |
| `Months` | `String` | **** | Meses del año que abarca este periodo escolar (ej. '[10,11,12,1,2,3,4,5]'). |
| `ScheduleType` | `String` | **** | Tipo de jornada escolar ('continua' o 'partida'). |
| `SlotMinutes` | `Integer` | **** | Duración de cada período o sesión lectiva (en minutos, ej. 45 o 60). |
| `SlotsPerDay` | `Integer` | **** | Número total de sesiones de clase al día. |
| `AfternoonSlots` | `Integer` | **** | Número de sesiones de clase que se imparten en el horario de tarde. |
| `IsDefault` | `Boolean` | **** | Indica si este periodo es el que se aplica por defecto durante el curso ordinario. |
| `SortOrder` | `Integer` | **** | Orden de prioridad para ordenar visualmente las etapas/periodos. |

### Tabla: `CycleSchedule`
**Propósito:** Configuración de la jornada diaria (hora de entrada, salida y distribución) adaptada a cada ciclo dentro de un periodo concreto.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `StageId` | `GUID` | **FK** | Clave foránea que asocia la fila con la etapa escolar (SchoolStage). |
| `PeriodId` | `GUID` | **FK** | Clave foránea que vincula con el periodo del calendario (SchoolPeriod). |
| `Cycle` | `Integer` | **** | Ciclo educativo al que pertenece (ej. 1 para primer ciclo, 2 para segundo ciclo, etc.). |
| `MorningStart` | `TimeOnly` | **** | Hora de inicio de las clases por la mañana. |
| `EndTime` | `TimeOnly` | **** | Hora de finalización de la jornada escolar diaria. |
| `AfternoonStart` | `TimeOnly (nullable)` | **** | Hora de inicio de las clases por la tarde (en jornada partida). |
| `Period` | `SchoolPeriod?` | **** | Objeto de navegación del periodo escolar (Ignorado en persistencia). |

### Tabla: `PeriodAssignmentHours`
**Propósito:** Horas semanales que corresponden a una asignación docente durante un periodo escolar específico.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `PeriodId` | `GUID` | **FK** | Clave foránea que vincula con el periodo del calendario (SchoolPeriod). |
| `AssignmentId` | `GUID` | **FK** | Clave foránea que vincula con la asignación de clase (Assignment). |
| `WeeklyHours` | `Integer` | **** | Total de horas semanales que se imparten en la asignación. |

### Tabla: `CycleBreak`
**Propósito:** Configura los recreos (inicio y duración) asociados a los ciclos dentro de cada periodo del calendario.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `CycleScheduleId` | `GUID` | **FK** | Clave foránea que vincula con la jornada del ciclo (CycleSchedule). |
| `AfterSlot` | `Integer` | **** | Sesión escolar tras la cual se sitúa el recreo (ej. tras la sesión 2). |
| `Minutes` | `Integer` | **** | Duración en minutos del recreo. |
| `Cycle` | `CycleSchedule?` | **** | Ciclo educativo al que pertenece (ej. 1 para primer ciclo, 2 para segundo ciclo, etc.). |

### Tabla: `SchoolStage`
**Propósito:** Etapa educativa (Infantil, Primaria, Secundaria) dentro de un colegio, con su configuración de jornada, recreos y cursos propios.

| Campo | Tipo | Clave | Descripción |
| --- | --- | --- | --- |
| `Id` | `GUID` | **PK** | Identificador único de la fila (GUID). |
| `SchoolId` | `GUID` | **FK** | Clave foránea que asocia la fila con el colegio (School). |
| `StageType` | `String` | **** | Tipo de etapa escolar ('infantil', 'primaria', 'secundaria'). |
| `Name` | `String` | **** | Nombre descriptivo de la entidad. |
| `MinLevel` | `Integer` | **** | Nivel lectivo mínimo para la etapa. |
| `MaxLevel` | `Integer` | **** | Nivel lectivo máximo para la etapa. |
| `SortOrder` | `Integer` | **** | Orden de prioridad para ordenar visualmente las etapas/periodos. |
| `ScheduleType` | `String` | **** | Tipo de jornada escolar ('continua' o 'partida'). |
| `MorningStart` | `TimeOnly` | **** | Hora de inicio de las clases por la mañana. |
| `AfternoonStart` | `TimeOnly (nullable)` | **** | Hora de inicio de las clases por la tarde (en jornada partida). |
| `SlotMinutes` | `Integer` | **** | Duración de cada período o sesión lectiva (en minutos, ej. 45 o 60). |
| `BreakAfterSlot` | `Integer` | **** | Número de sesión lectiva tras la cual se realiza el descanso/recreo (ej. tras la 2ª sesión). |
| `BreakMinutes` | `Integer` | **** | Duración en minutos del recreo o descanso general. |
| `SlotsPerDay` | `Integer` | **** | Número total de sesiones de clase al día. |
| `AfternoonSlots` | `Integer` | **** | Número de sesiones de clase que se imparten en el horario de tarde. |
| `DaysPerWeek` | `Integer` | **** | Número de días lectivos a la semana (normalmente 5). |
| `WorkingDays` | `String` | **** | Días laborables de la semana representados en formato JSON array (ej. '[1,2,3,4,5]'). |
