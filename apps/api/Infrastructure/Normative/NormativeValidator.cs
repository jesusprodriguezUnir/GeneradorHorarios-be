using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Infrastructure.Normative;

/// <summary>
/// Implementación del validador normativo.
/// Comprueba que la configuración del centro cumple el Decreto 61/2022 de Madrid.
/// </summary>
public sealed class NormativeValidator : INormativeValidator
{
    public Task<List<ConflictExplanation>> ValidateAsync(
        School school,
        IReadOnlyList<(Assignment Assignment, SubjectAllocation Allocation)> assignmentData,
        CancellationToken ct = default)
    {
        var issues = new List<ConflictExplanation>();

        ValidateSchoolConfig(school, issues);
        ValidateGroupHours(school, assignmentData, issues);

        return Task.FromResult(issues);
    }

    // ── Validaciones de configuración del centro ─────────────────────────────────

    private static void ValidateSchoolConfig(School school, List<ConflictExplanation> issues)
    {
        // Etapa: solo Primaria (niveles 1-6)
        if (!string.Equals(school.Stage, LomloeMadrid.Stage, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(MakeIssue(ConflictSeverity.Warning,
                $"La etapa configurada es '{school.Stage}', pero este validador solo cubre Educación Primaria.",
                ["Comprueba que la etapa del centro esté establecida a 'primaria'."]));
        }
        else
        {
            if (school.MinCourseLevel < LomloeMadrid.PrimariaMinLevel)
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El nivel mínimo del centro ({school.MinCourseLevel}) es inferior al permitido para primaria ({LomloeMadrid.PrimariaMinLevel}).",
                    ["Establece MinCourseLevel = 1."]));

            if (school.MaxCourseLevel > LomloeMadrid.PrimariaMaxLevel)
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El nivel máximo del centro ({school.MaxCourseLevel}) supera el de primaria ({LomloeMadrid.PrimariaMaxLevel} cursos).",
                    ["Establece MaxCourseLevel ≤ 6 para primaria."]));
        }

        // Recreo mínimo diario: 30 minutos (Decreto 61/2022 y Decreto 94/2025)
        if (school.BreakMinutes < LomloeMadrid.MinDailyBreakMinutes)
        {
            issues.Add(MakeIssue(ConflictSeverity.Error,
                $"El recreo configurado ({school.BreakMinutes} min) es inferior al mínimo legal de {LomloeMadrid.MinDailyBreakMinutes} min/día " +
                $"(Decreto 61/2022, Decreto 94/2025).",
                [
                    $"Establece BreakMinutes ≥ {LomloeMadrid.MinDailyBreakMinutes}.",
                    "El recreo no cuenta como tiempo lectivo — debe añadirse a las 22,5 h semanales.",
                ]));
        }

        // Total lectivo semanal mínimo: 22,5 h = 22 sesiones de 60 min (o proporcional)
        decimal lectiveMinutes = (decimal)school.SlotsPerDay * school.SlotMinutes * school.DaysPerWeek;
        decimal lectiveHours   = lectiveMinutes / 60m;

        if (lectiveHours < LomloeMadrid.MinWeeklyLectiveHours)
        {
            issues.Add(MakeIssue(ConflictSeverity.Error,
                $"La rejilla horaria del centro ({lectiveHours:F1} h/semana lectivas) es inferior al " +
                $"mínimo legal de {LomloeMadrid.MinWeeklyLectiveHours} h/semana (Decreto 61/2022).",
                [
                    $"Aumenta SlotsPerDay o DaysPerWeek para alcanzar ≥ {LomloeMadrid.MinWeeklyLectiveHours} h lectivas/semana.",
                    "Por ejemplo: 5 tramos de 60 min × 5 días = 25 h/semana.",
                ]));
        }
    }

    // ── Validaciones por grupo ────────────────────────────────────────────────────

    private static void ValidateGroupHours(
        School school,
        IReadOnlyList<(Assignment Assignment, SubjectAllocation Allocation)> data,
        List<ConflictExplanation> issues)
    {
        // Agrupar asignaciones por grupo
        var byGroup = data
            .GroupBy(d => d.Assignment.GroupId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        // Capacidad máxima de la rejilla (h/semana)
        decimal gridCapacityHours = (decimal)school.SlotsPerDay * school.SlotMinutes * school.DaysPerWeek / 60m;

        // Inferir modalidad a partir de las horas de inglés asignadas (heurística)
        // Un grupo que asigna 5 h de inglés se trata como bilingüe
        foreach (var (groupId, assignments) in byGroup)
        {
            decimal totalH = assignments.Sum(a => (decimal)a.Assignment.WeeklyHours);

            // ── Total lectivo ──────────────────────────────────────────────────
            if (totalH < LomloeMadrid.MinWeeklyLectiveHours)
            {
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El grupo {GroupLabel(assignments)} solo tiene {totalH:F0} h/semana asignadas, " +
                    $"por debajo del mínimo legal de {LomloeMadrid.MinWeeklyLectiveHours} h (Decreto 61/2022).",
                    [
                        "Añade asignaciones de asignaturas para completar al menos 22,5 h lectivas/semana.",
                        "Consulta la tabla del Decreto 61/2022 (Anexo IV) para los mínimos por área.",
                    ],
                    groupId: groupId));
            }
            else if (totalH > gridCapacityHours)
            {
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El grupo {GroupLabel(assignments)} tiene {totalH:F0} h/semana asignadas, " +
                    $"pero la rejilla solo tiene capacidad para {gridCapacityHours:F0} h. " +
                    "El motor no podrá generar un horario completo.",
                    [
                        $"Reduce las horas totales de las asignaciones a ≤ {gridCapacityHours:F0} h/semana.",
                        "Ten en cuenta que el recreo NO cuenta como hora lectiva.",
                    ],
                    groupId: groupId));
            }

            // Inferir modalidad desde horas de inglés
            int ingHours = assignments
                .Where(a => a.Allocation.SubjectKey == "ing")
                .Sum(a => a.Assignment.WeeklyHours);
            string modality = ingHours >= 5 ? "bilingue" : "estandar";
            var norms = LomloeMadrid.GetSubjects(modality);

            // ── Mínimos por área ───────────────────────────────────────────────
            foreach (var norm in norms.Where(n => n.MinH > 0))
            {
                int assignedH = assignments
                    .Where(a => a.Allocation.SubjectKey == norm.SubjectKey)
                    .Sum(a => a.Assignment.WeeklyHours);

                if (assignedH < norm.MinH)
                {
                    issues.Add(MakeIssue(ConflictSeverity.Warning,
                        $"El grupo {GroupLabel(assignments)} tiene {assignedH} h/semana de {norm.SubjectName}, " +
                        $"por debajo del mínimo recomendado de {norm.MinH} h según el Decreto 61/2022.",
                        [
                            $"Considera asignar al menos {norm.MinH} h de {norm.SubjectName} a este grupo.",
                            $"El margen es {norm.MinH}-{norm.MaxH} h/semana.",
                        ],
                        groupId: groupId));
                }
                else if (assignedH > norm.MaxH)
                {
                    issues.Add(MakeIssue(ConflictSeverity.Warning,
                        $"El grupo {GroupLabel(assignments)} tiene {assignedH} h/semana de {norm.SubjectName}, " +
                        $"superando el máximo de {norm.MaxH} h según el Decreto 61/2022.",
                        [
                            $"Reduce las horas de {norm.SubjectName} a ≤ {norm.MaxH} h/semana.",
                        ],
                        groupId: groupId));
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string GroupLabel(
        IEnumerable<(Assignment Assignment, SubjectAllocation Allocation)> assignments)
    {
        // Intenta construir una etiqueta legible para el grupo
        var firstAssignment = assignments.FirstOrDefault();
        return firstAssignment.Assignment is not null
            ? firstAssignment.Assignment.GroupId.ToString()[..8] + "…"
            : "desconocido";
    }

    private static ConflictExplanation MakeIssue(
        ConflictSeverity severity,
        string description,
        IReadOnlyList<string>? suggestions = null,
        Guid? groupId = null,
        Guid? teacherId = null)
    {
        return new ConflictExplanation
        {
            Type        = ConflictType.Normative,
            Severity    = severity,
            Description = description,
            Suggestions = suggestions ?? [],
            GroupId     = groupId,
            TeacherId   = teacherId,
        };
    }
}
