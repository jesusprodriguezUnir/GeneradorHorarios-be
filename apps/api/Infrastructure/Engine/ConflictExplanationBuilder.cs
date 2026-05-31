using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Engine;

/// <summary>
/// Genera explicaciones en lenguaje natural de por qué no se pudo asignar una sesión.
/// Implementa el principio P2 de la constitución: explicabilidad sobre perfección.
/// </summary>
public static class ConflictExplanationBuilder
{
    public static ConflictExplanation Build(
        SessionToAssign session,
        List<AssignedSlot> assigned,
        GenerationContext context)
    {
        // ── Detectar causa más probable ───────────────────────────────────────

        // ¿Profesor sobrecargado?
        int teacherSlotsUsed = assigned.Count(a => a.TeacherId == session.TeacherId);
        int maxSlots = context.School.SlotsPerDay * 5;   // 5 días
        if (teacherSlotsUsed >= maxSlots - 2)
        {
            return new ConflictExplanation
            {
                Type = ConflictType.Teacher,
                Severity = ConflictSeverity.Error,
                Description = $"No fue posible asignar '{session.SubjectName}' al grupo {session.GroupLabel}: el profesor ya tiene {teacherSlotsUsed} sesiones asignadas (límite del horario alcanzado).",
                Suggestions =
                [
                    "Reduce las horas semanales de este profesor en otras asignaturas.",
                    "Asigna esta asignatura a otro profesor con disponibilidad.",
                    "Revisa que las asignaciones totales del colegio no superen la capacidad del horario.",
                ],
            };
        }

        // ¿Requiere aula especial sin disponibilidad?
        if (session.RequiredClassroomType.HasValue)
        {
            return new ConflictExplanation
            {
                Type = ConflictType.Classroom,
                Severity = ConflictSeverity.Error,
                Description = $"No fue posible asignar '{session.SubjectName}' al grupo {session.GroupLabel}: no hay disponibilidad en aulas de tipo '{session.RequiredClassroomType}' para los horarios disponibles del profesor.",
                Suggestions =
                [
                    $"Añade más aulas de tipo '{session.RequiredClassroomType}' en la Configuración → Aulas.",
                    "Revisa si el aula especial está ocupada en todos los tramos por otros grupos.",
                    "Considera si esta asignatura puede impartirse en un aula ordinaria.",
                ],
            };
        }

        // ¿Conflicto normativo?
        int weeklyAssigned = assigned.Count(a =>
            a.TeacherId == session.TeacherId &&
            a.AllocationId == session.AllocationId);
        if (weeklyAssigned > 0)
        {
            return new ConflictExplanation
            {
                Type = ConflictType.Normative,
                Severity = ConflictSeverity.Warning,
                Description = $"La asignatura '{session.SubjectName}' para el grupo {session.GroupLabel} tiene {weeklyAssigned} sesiones asignadas pero se requería una más. Posible colisión de horarios.",
                Suggestions =
                [
                    "Revisa las restricciones de disponibilidad del profesor.",
                    "Verifica que no haya solapamientos con otros grupos del mismo profesor.",
                ],
            };
        }

        // Genérico
        return new ConflictExplanation
        {
            Type = ConflictType.Teacher,
            Severity = ConflictSeverity.Error,
            Description = $"No fue posible asignar '{session.SubjectName}' al grupo {session.GroupLabel}. El algoritmo agotó todas las combinaciones posibles dentro del tiempo límite.",
            Suggestions =
            [
                "Aumenta el tiempo límite de generación (actualmente 30s).",
                "Reduce el número de restricciones de disponibilidad.",
                "Verifica que las horas totales asignadas no superen la capacidad del horario.",
            ],
        };
    }
}
