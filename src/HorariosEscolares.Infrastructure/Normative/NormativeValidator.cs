using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Normative;

public sealed class NormativeValidator : INormativeValidator
{
    public Task<List<ConflictExplanation>> ValidateAsync(
        NormativeValidationData data,
        CancellationToken ct = default)
    {
        var issues = new List<ConflictExplanation>();

        ValidateSchoolConfig(data, issues);
        ValidateGroupHours(data, issues);

        return Task.FromResult(issues);
    }

    private static void ValidateSchoolConfig(NormativeValidationData data, List<ConflictExplanation> issues)
    {
        if (!string.Equals(data.Stage, LomloeMadrid.Stage, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(MakeIssue(ConflictSeverity.Warning,
                $"La etapa configurada es '{data.Stage}', pero este validador solo cubre Educación Primaria.",
                ["Comprueba que la etapa del centro esté establecida a 'primaria'."]));
        }
        else
        {
            if (data.MinCourseLevel < LomloeMadrid.PrimariaMinLevel)
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El nivel mínimo del centro ({data.MinCourseLevel}) es inferior al permitido para primaria ({LomloeMadrid.PrimariaMinLevel}).",
                    ["Establece MinCourseLevel = 1."]));

            if (data.MaxCourseLevel > LomloeMadrid.PrimariaMaxLevel)
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El nivel máximo del centro ({data.MaxCourseLevel}) supera el de primaria ({LomloeMadrid.PrimariaMaxLevel} cursos).",
                    ["Establece MaxCourseLevel ≤ 6 para primaria."]));
        }

        if (data.BreakMinutes < LomloeMadrid.MinDailyBreakMinutes)
        {
            issues.Add(MakeIssue(ConflictSeverity.Error,
                $"El recreo configurado ({data.BreakMinutes} min) es inferior al mínimo legal de {LomloeMadrid.MinDailyBreakMinutes} min/día " +
                $"(Decreto 61/2022, Decreto 94/2025).",
                [
                    $"Establece BreakMinutes ≥ {LomloeMadrid.MinDailyBreakMinutes}.",
                    "El recreo no cuenta como tiempo lectivo — debe añadirse a las 22,5 h semanales.",
                ]));
        }

        decimal lectiveMinutes = (decimal)data.SchoolConfig.SlotsPerDay * data.SlotMinutes * data.SchoolConfig.DaysPerWeek;
        decimal lectiveHours = lectiveMinutes / 60m;

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

    private static void ValidateGroupHours(NormativeValidationData data, List<ConflictExplanation> issues)
    {
        var byGroup = data.Assignments
            .GroupBy(d => d.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (groupId, assignments) in byGroup)
        {
            decimal totalH = assignments.Sum(a => (decimal)a.WeeklyHours);

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

            int ingHours = assignments
                .Where(a => a.SubjectKey == "ing")
                .Sum(a => a.WeeklyHours);
            string modality = ingHours >= 5 ? "bilingue" : "estandar";
            var norms = LomloeMadrid.GetSubjects(modality);

            foreach (var norm in norms.Where(n => n.MinH > 0))
            {
                int assignedH = assignments
                    .Where(a => a.SubjectKey == norm.SubjectKey)
                    .Sum(a => a.WeeklyHours);

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

    private static string GroupLabel(IReadOnlyList<NormativeAssignmentData> assignments)
    {
        var firstAssignment = assignments.FirstOrDefault();
        return firstAssignment is not null
            ? firstAssignment.GroupId.ToString()[..8] + "…"
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
            Type = ConflictType.Normative,
            Severity = severity,
            Description = description,
            Suggestions = suggestions ?? [],
            GroupId = groupId,
            TeacherId = teacherId,
        };
    }
}
