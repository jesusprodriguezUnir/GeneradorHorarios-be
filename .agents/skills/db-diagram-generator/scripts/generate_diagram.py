import os
import re

# Paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# Navigate up from .agents/skills/db-diagram-generator/scripts/ to the repository root
BASE_DIR = SCRIPT_DIR
for _ in range(4):
    BASE_DIR = os.path.dirname(BASE_DIR)

ENTITIES_FILE = os.path.join(BASE_DIR, "src", "HorariosEscolares.Domain", "Entities", "PersistenceEntities.cs")
OUTPUT_FILE = os.path.join(BASE_DIR, "docs", "db_schema_diagram.md")

# Dictionary of Spanish descriptions for tables
TABLE_DESCRIPTIONS = {
    "School": "Configuración general y datos básicos de un centro escolar (colegio), como nombre, código, año académico, jornada general y días laborables.",
    "SchoolStage": "Etapa educativa (Infantil, Primaria, Secundaria) dentro de un colegio, con su configuración de jornada, recreos y cursos propios.",
    "AppUser": "Usuarios con acceso a la aplicación, indicando su correo, nombre completo, rol asignado y su vinculación con un profesor físico.",
    "Teacher": "Personal docente del colegio. Contiene información sobre sus límites de horas semanales y celdas consecutivas al día, sus especialidades y color asignado.",
    "TeacherStageAssignment": "Asociación de un profesor con una etapa educativa (Infantil, Primaria, Secundaria) y opcionalmente con un ciclo concreto, limitando dónde puede dar clase.",
    "Classroom": "Espacios físicos y aulas del colegio (aulas ordinarias de grupo, aulas específicas de música, gimnasio, etc.), su capacidad y si es compartida.",
    "CurriculumTemplate": "Plantillas curriculares que definen la estructura general de materias y horas oficiales vigentes para una etapa educativa y región.",
    "SubjectAllocation": "Configuración y restricciones específicas para una asignatura de la plantilla curricular (ej. Matemáticas, Lengua), como horas mínimas/máximas y si requiere especialista.",
    "CourseGroup": "Grupos de alumnos de un nivel y letra concretos (ej. 1º A, 3º B), asociados a un tutor de referencia y a su aula principal.",
    "GroupSubjectHour": "Especificación de las horas lectivas que un grupo en concreto debe recibir de una asignatura determinada.",
    "Assignment": "Asignación de docencia. Vincula a un profesor con un grupo de alumnos y una asignatura para impartir un número de horas semanales.",
    "TeacherConstraint": "Restricciones horarias de los profesores (ej. no dar clase los viernes por la tarde, mañanas libres por reducciones, etc.).",
    "ScheduleRecord": "Cabecera del horario de una etapa escolar. Registra el estado (borrador, publicado), la fecha de generación y estadísticas generales.",
    "ScheduleEntry": "Cada una de las sesiones de clase asignadas en la cuadrícula horaria. Conecta un día y hora con profesor, grupo, asignatura y aula.",
    "ScheduleConflictRecord": "Conflictos y advertencias detectados por el motor de horarios al validar el horario actual (ej. colisiones de profesores o aulas).",
    "SchoolPeriod": "Periodos lectivos específicos del año (ej. Jornada continua en Septiembre/Junio vs Jornada partida el resto del año).",
    "CycleSchedule": "Configuración de la jornada diaria (hora de entrada, salida y distribución) adaptada a cada ciclo dentro de un periodo concreto.",
    "CycleBreak": "Configura los recreos (inicio y duración) asociados a los ciclos dentro de cada periodo del calendario.",
    "PeriodAssignmentHours": "Horas semanales que corresponden a una asignación docente durante un periodo escolar específico."
}

# Dictionary of field descriptions in Spanish
FIELD_DESCRIPTIONS = {
    "Id": "Identificador único de la fila (GUID).",
    "SchoolId": "Clave foránea que asocia la fila con el colegio (School).",
    "StageId": "Clave foránea que asocia la fila con la etapa escolar (SchoolStage).",
    "TeacherId": "Clave foránea que vincula con el profesor (Teacher).",
    "GroupId": "Clave foránea que vincula con el grupo (CourseGroup).",
    "AllocationId": "Clave foránea que vincula con la asignatura/materia (SubjectAllocation).",
    "ClassroomId": "Clave foránea que vincula con el aula (Classroom).",
    "PeriodId": "Clave foránea que vincula con el periodo del calendario (SchoolPeriod).",
    "TemplateId": "Clave foránea que vincula con la plantilla curricular (CurriculumTemplate).",
    "CycleScheduleId": "Clave foránea que vincula con la jornada del ciclo (CycleSchedule).",
    "AssignmentId": "Clave foránea que vincula con la asignación de clase (Assignment).",
    
    # School / Stage fields
    "StageType": "Tipo de etapa escolar ('infantil', 'primaria', 'secundaria').",
    "CenterCode": "Código oficial del centro escolar asignado por la Consejería de Educación.",
    "Locality": "Localidad / Municipio donde se ubica el colegio.",
    "Community": "Comunidad Autónoma a la que pertenece el centro (por defecto, 'madrid').",
    "Stage": "Etapa educativa principal (por defecto, 'primaria').",
    "MinCourseLevel": "Curso académico mínimo de la etapa (ej. 1 para Primero de Primaria).",
    "MaxCourseLevel": "Curso académico máximo de la etapa (ej. 6 para Sexto de Primaria).",
    "MinLevel": "Nivel lectivo mínimo para la etapa.",
    "MaxLevel": "Nivel lectivo máximo para la etapa.",
    "SortOrder": "Orden de prioridad para ordenar visualmente las etapas/periodos.",
    
    # Generic names / descriptions
    "Name": "Nombre descriptivo de la entidad.",
    "Slug": "Identificador amigable en la URL.",
    "Email": "Dirección de correo electrónico (única).",
    "FullName": "Nombre y apellidos completos.",
    "Role": "Rol del usuario (ej. 'admin', 'teacher').",
    "AcademicYear": "Curso escolar correspondiente (ej. '2025/2026').",
    "CreatedAt": "Fecha y hora de creación del registro en el sistema.",
    
    # Time / Schedule fields
    "ScheduleType": "Tipo de jornada escolar ('continua' o 'partida').",
    "MorningStart": "Hora de inicio de las clases por la mañana.",
    "EndTime": "Hora de finalización de la jornada escolar diaria.",
    "AfternoonStart": "Hora de inicio de las clases por la tarde (en jornada partida).",
    "SlotMinutes": "Duración de cada período o sesión lectiva (en minutos, ej. 45 o 60).",
    "BreakAfterSlot": "Número de sesión lectiva tras la cual se realiza el descanso/recreo (ej. tras la 2ª sesión).",
    "BreakMinutes": "Duración en minutos del recreo o descanso general.",
    "SlotsPerDay": "Número total de sesiones de clase al día.",
    "AfternoonSlots": "Número de sesiones de clase que se imparten en el horario de tarde.",
    "DaysPerWeek": "Número de días lectivos a la semana (normalmente 5).",
    "WorkingDays": "Días laborables de la semana representados en formato JSON array (ej. '[1,2,3,4,5]').",
    
    # Limits & Parameters
    "MaxWeeklyHours": "Límite máximo de horas lectivas semanales que puede impartir.",
    "MaxDailyConsecutive": "Límite máximo de sesiones consecutivas de clase que puede dar al día sin descanso.",
    "Specialties": "Especialidades del docente en formato JSON array (ej. ['ingles', 'musica']).",
    "ColorKey": "Código de color hexadecimal o abreviatura visual para la interfaz gráfica.",
    "WeeklyHours": "Total de horas semanales que se imparten en la asignación.",
    "Hours": "Horas semanales de la materia para el grupo.",
    "ConstraintType": "Tipo de restricción de disponibilidad (ej. 'unavailable', 'preferred').",
    "DayOfWeek": "Día de la semana (1 = Lunes, 5 = Viernes).",
    "SlotIndex": "Índice del periodo horario en el día (0 = primera sesión).",
    "Status": "Estado actual del horario (ej. 'draft', 'published').",
    
    # Tutor / Classroom
    "TutorId": "Clave foránea del profesor que ejerce la tutoría del grupo.",
    "HomeClassroomId": "Clave foránea del aula asignada por defecto a este grupo (aula de referencia).",
    "Capacity": "Capacidad máxima de alumnos que admite el aula física.",
    "IsShared": "Indica si el aula puede ser compartida por varios grupos simultáneamente.",
    
    # Subject Allocation
    "SubjectName": "Nombre completo de la asignatura (ej. 'Matemáticas').",
    "SubjectShort": "Nombre abreviado o siglas de la asignatura (ej. 'MAT').",
    "SubjectKey": "Código corto identificador único de la asignatura (ej. 'mat', 'len').",
    "WeeklyHoursMin": "Horas mínimas recomendadas a la semana para esta materia.",
    "WeeklyHoursMax": "Horas máximas permitidas a la semana para esta materia.",
    "WeeklyHoursDefault": "Horas semanales por defecto indicadas para la materia.",
    "RequiresSpecialist": "Indica si la materia debe ser impartida obligatoriamente por un profesor especialista.",
    "RequiredClassroomType": "Tipo de aula requerido si es específico (ej. 'musica', 'gimnasio').",
    "MaxConsecutiveSlots": "Número máximo de sesiones consecutivas permitidas para esta materia en un mismo día (ej. 2).",
    "SplittableAcrossDays": "Indica si las horas semanales de esta materia se pueden repartir en diferentes días.",
    "StudentCount": "Número total de alumnos matriculados en el grupo.",
    "GroupLabel": "Letra identificadora del grupo (ej. 'A', 'B').",
    "CourseLevel": "Nivel educativo del grupo (ej. 1 para 1º, 6 para 6º).",
    
    # Schedule Records
    "GeneratedAt": "Fecha y hora en que se completó la generación del horario.",
    "PublishedAt": "Fecha y hora de publicación oficial del horario.",
    "GenerationSeconds": "Tiempo total (en segundos) empleado por el motor de backtracking en resolver el horario.",
    "TotalConflicts": "Número total de conflictos (errores duros) presentes en este horario.",
    "CreatedBy": "Clave foránea del usuario que creó e inició la generación del horario.",
    
    # Schedule entries
    "IsManualOverride": "Indica si esta celda del horario se ha fijado manualmente y no debe ser alterada por el motor de generación.",
    "ConflictType": "Tipo de conflicto detectado (ej. 'Teacher', 'Classroom', 'Normative').",
    "Severity": "Severidad del problema ('Error' para restricciones duras o 'Warning' para blandas).",
    "Description": "Descripción detallada del conflicto encontrado.",
    "Suggestions": "Lista de sugerencias de solución propuestas en formato JSON array.",
    
    # Periods & Breaks
    "Key": "Código identificativo del periodo escolar (ej. 'ordinario', 'septiembre_junio').",
    "Months": "Meses del año que abarca este periodo escolar (ej. '[10,11,12,1,2,3,4,5]').",
    "IsDefault": "Indica si este periodo es el que se aplica por defecto durante el curso ordinario.",
    "AfterSlot": "Sesión escolar tras la cual se sitúa el recreo (ej. tras la sesión 2).",
    "Minutes": "Duración en minutos del recreo.",
    "Cycle": "Ciclo educativo al que pertenece (ej. 1 para primer ciclo, 2 para segundo ciclo, etc.).",
    
    # Extra entities fields
    "Teacher": "Objeto de navegación del profesor (Ignorado en persistencia).",
    "Stage": "Objeto de navegación de la etapa escolar (Ignorado en persistencia).",
    "Period": "Objeto de navegación del periodo escolar (Ignorado en persistencia).",
    "Group": "Objeto de navegación del grupo al que pertenece (Ignorado en persistencia)."
}

def clean_csharp_type(t):
    # Simplify common C# types to standard names
    t = t.strip()
    if t.startswith("DbSet<"):
        t = t[6:-1]
    if t.startswith("ICollection<"):
        t = t[12:-1]
    if t == "Guid":
        return "GUID"
    if t == "Guid?":
        return "GUID (nullable)"
    if t == "string":
        return "String"
    if t == "string?":
        return "String (nullable)"
    if t == "int":
        return "Integer"
    if t == "int?":
        return "Integer (nullable)"
    if t == "bool":
        return "Boolean"
    if t == "DateTime":
        return "DateTime"
    if t == "DateTime?":
        return "DateTime (nullable)"
    if t == "TimeOnly":
        return "TimeOnly"
    if t == "TimeOnly?":
        return "TimeOnly (nullable)"
    return t

def parse_entities():
    if not os.path.exists(ENTITIES_FILE):
        print(f"Error: Entities file not found at {ENTITIES_FILE}")
        return []

    with open(ENTITIES_FILE, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find all classes
    # Matches: public class ClassName { ... }
    # Since classes can contain properties, let's use a simpler tokenized parser or split
    classes = re.findall(r'public class (\w+)\s*(?::\s*\w+\s*)?\n\s*\{([\s\S]*?)\n\s*\}', content)
    
    parsed_classes = []
    for class_name, body in classes:
        # Ignore static configuration classes or helper classes like StageTypes
        if class_name in ["StageTypes"]:
            continue
            
        properties = []
        # Find properties like: public required string Name { get; set; }
        # or public Guid Id { get; set; } = Guid.NewGuid();
        prop_matches = re.finditer(r'public\s+(?:required\s+)?([\w\?<>]+)\s+(\w+)\s*\{\s*get;\s*(?:set|private\s+set);\s*\}', body)
        for pm in prop_matches:
            prop_type = clean_csharp_type(pm.group(1))
            prop_name = pm.group(2)
            
            # Skip navigation collections or virtual reference objects in the attributes view
            # to keep the ER diagram simple and focused on table columns
            if "List" in prop_type or "Collection" in prop_type or prop_type in TABLE_DESCRIPTIONS:
                continue
            
            properties.append({
                "name": prop_name,
                "type": prop_type
            })
            
        parsed_classes.append({
            "name": class_name,
            "properties": properties
        })
        
    return parsed_classes

def generate_mermaid(parsed_classes):
    mermaid = []
    mermaid.append("```mermaid")
    mermaid.append("erDiagram")
    
    # 1. Define entities with their attributes
    for c in parsed_classes:
        mermaid.append(f"    {c['name']} {{")
        for p in c['properties']:
            pk_fk = ""
            if p['name'] == "Id":
                pk_fk = "PK"
            elif p['name'].endswith("Id") and p['name'] != "Id":
                pk_fk = "FK"
            mermaid.append(f"        {p['type']} {p['name']} {pk_fk}")
        mermaid.append("    }")
        mermaid.append("")
        
    # 2. Hardcode relationships for cleanliness and accuracy
    relationships = [
        ("School", "SchoolStage", "||--o{", "tiene"),
        ("SchoolStage", "CourseGroup", "||--o{", "tiene"),
        ("SchoolStage", "TeacherStageAssignment", "||--o{", "filtra profesores"),
        ("School", "Teacher", "||--o{", "tiene"),
        ("Teacher", "TeacherStageAssignment", "||--o{", "etapas asignadas"),
        ("School", "Classroom", "||--o{", "tiene"),
        ("SchoolStage", "SchoolPeriod", "||--o{", "tiene"),
        ("SchoolPeriod", "CycleSchedule", "||--o{", "ciclos del periodo"),
        ("CycleSchedule", "CycleBreak", "||--o{", "recreos del ciclo"),
        ("CurriculumTemplate", "SubjectAllocation", "||--o{", "materias configuradas"),
        ("SchoolStage", "CurriculumTemplate", "||--o{", "plantilla curricular"),
        ("CourseGroup", "GroupSubjectHour", "||--o{", "horas por materia"),
        ("CourseGroup", "Teacher", "|o--o|", "tutor"),
        ("CourseGroup", "Classroom", "|o--o|", "aula habitual"),
        ("Assignment", "PeriodAssignmentHours", "||--o{", "horas por periodo"),
        ("SchoolPeriod", "PeriodAssignmentHours", "||--o{", "periodo de la asignacion"),
        ("Teacher", "Assignment", "||--o{", "tiene asignaciones"),
        ("CourseGroup", "Assignment", "||--o{", "tiene asignaciones"),
        ("SubjectAllocation", "Assignment", "||--o{", "tiene asignaciones"),
        ("School", "Assignment", "||--o{", "tiene asignaciones"),
        ("Teacher", "TeacherConstraint", "||--o{", "restricciones de horario"),
        ("School", "TeacherConstraint", "||--o{", "restricciones de horario"),
        ("ScheduleRecord", "ScheduleEntry", "||--o{", "entradas del horario"),
        ("ScheduleRecord", "ScheduleConflictRecord", "||--o{", "conflictos del horario"),
        ("ScheduleRecord", "SchoolPeriod", "} |--||", "periodo del horario"),
        ("ScheduleEntry", "CourseGroup", "} |--||", "grupo"),
        ("ScheduleEntry", "SubjectAllocation", "} |--||", "materia"),
        ("ScheduleEntry", "Teacher", "} |--||", "profesor"),
        ("ScheduleEntry", "Classroom", "} |--||", "aula")
    ]
    
    for left, right, rel, desc in relationships:
        # Verify both exist in parsed classes to avoid Mermaid syntax crash
        class_names = [c['name'] for c in parsed_classes]
        if left in class_names and right in class_names:
            mermaid.append(f"    {left} {rel} {right} : \"{desc}\"")
            
    mermaid.append("```")
    return "\n".join(mermaid)

def generate_markdown(parsed_classes, mermaid_diagram):
    md = []
    md.append("# Estructura de la Base de Datos - Lectivo")
    md.append("")
    md.append("Este documento describe el esquema de la base de datos de **Lectivo**, la aplicación para la generación automática de horarios escolares.")
    md.append("Este archivo se genera automáticamente analizando las entidades de persistencia C# mediante la Skill `db-diagram-generator`.")
    md.append("")
    md.append("## Diagrama Entidad-Relación (Mermaid)")
    md.append("")
    md.append(mermaid_diagram)
    md.append("")
    md.append("## Guía y Explicación de las Tablas (Español)")
    md.append("")
    md.append("A continuación se presenta una explicación detallada del propósito de cada tabla y sus campos clave:")
    md.append("")
    
    for c in parsed_classes:
        name = c['name']
        desc = TABLE_DESCRIPTIONS.get(name, "Sin descripción disponible.")
        md.append(f"### Tabla: `{name}`")
        md.append(f"**Propósito:** {desc}")
        md.append("")
        md.append("| Campo | Tipo | Clave | Descripción |")
        md.append("| --- | --- | --- | --- |")
        
        for p in c['properties']:
            field_name = p['name']
            field_type = p['type']
            
            # Key type
            key_type = ""
            if field_name == "Id":
                key_type = "PK"
            elif field_name.endswith("Id") and field_name != "Id":
                key_type = "FK"
                
            # Description translation/fallback
            field_desc = FIELD_DESCRIPTIONS.get(field_name, "")
            if not field_desc:
                if field_name.endswith("Id") and field_name != "Id":
                    referenced_table = field_name[:-2]
                    field_desc = f"Clave foránea que vincula esta fila con la tabla `{referenced_table}`."
                else:
                    field_desc = "-"
                    
            md.append(f"| `{field_name}` | `{field_type}` | **{key_type}** | {field_desc} |")
        md.append("")
        
    return "\n".join(md)

def main():
    print("Iniciando generación de diagrama...")
    classes = parse_entities()
    if not classes:
        print("No se encontraron clases para procesar.")
        return
        
    print(f"Encontradas {len(classes)} entidades. Generando Mermaid...")
    mermaid = generate_mermaid(classes)
    
    print("Construyendo documentación Markdown...")
    markdown_content = generate_markdown(classes, mermaid)
    
    # Ensure docs directory exists
    docs_dir = os.path.dirname(OUTPUT_FILE)
    if not os.path.exists(docs_dir):
        os.makedirs(docs_dir)
        
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        f.write(markdown_content)
        
    print(f"Documentación guardada con éxito en: {OUTPUT_FILE}")

if __name__ == "__main__":
    main()
