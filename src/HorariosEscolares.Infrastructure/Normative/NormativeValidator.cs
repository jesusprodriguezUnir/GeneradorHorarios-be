using HorariosEscolares.Domain.Normative;
using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Normative;

public sealed class NormativeValidator(INormativeStageRegistry registry) : INormativeValidator
{
    public Task<List<ConflictExplanation>> ValidateAsync(
        NormativeValidationData data,
        CancellationToken ct = default)
    {
        var issues = new List<ConflictExplanation>();

        var normative = registry.Resolve(data.Stage);
        if (normative is null)
        {
            issues.Add(MakeIssue(ConflictSeverity.Warning,
                $"La etapa configurada es '{data.Stage}', pero no hay normativa registrada para validarla.",
                ["Comprueba que la etapa del centro sea una de: infantil, primaria, secundaria."]));
            return Task.FromResult(issues);
        }

        ValidateSchoolConfig(data, normative, issues);
        ValidateGroupHours(data, normative, issues);

        return Task.FromResult(issues);
    }

    private static void ValidateSchoolConfig(
        NormativeValidationData data, IStageNormative normative, List<ConflictExplanation> issues)
    {
        if (data.MinCourseLevel < normative.MinLevel)
            issues.Add(MakeIssue(ConflictSeverity.Error,
                $"El nivel mínimo del centro ({data.MinCourseLevel}) es inferior al permitido para {normative.StageType} ({normative.MinLevel}).",
                [$"Establece MinCourseLevel = {normative.MinLevel}."]));

        if (data.MaxCourseLevel > normative.MaxLevel)
            issues.Add(MakeIssue(ConflictSeverity.Error,
                $"El nivel máximo del centro ({data.MaxCourseLevel}) supera el de {normative.StageType} ({normative.MaxLevel} cursos).",
                [$"Establece MaxCourseLevel ≤ {normative.MaxLevel} para {normative.StageType}."]));

        if (data.BreakMinutes < normative.MinDailyBreakMinutes)
        {
            issues.Add(MakeIssue(ConflictSeverity.Error,
                $"El recreo configurado ({data.BreakMinutes} min) es inferior al mínimo legal de {normative.MinDailyBreakMinutes} min/día.",
                [
                    $"Establece BreakMinutes ≥ {normative.MinDailyBreakMinutes}.",
                    "El recreo no cuenta como tiempo lectivo — debe añadirse a las horas lectivas semanales.",
                ]));
        }

        if (data.EnforceWeeklyLectiveMinimum)
        {
            decimal lectiveMinutes = (decimal)data.SchoolConfig.SlotsPerDay * data.SlotMinutes * data.SchoolConfig.DaysPerWeek;
            decimal lectiveHours = lectiveMinutes / 60m;

            if (lectiveHours < normative.MinWeeklyLectiveHours)
            {
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"La rejilla horaria del centro ({lectiveHours:F1} h/semana lectivas) es inferior al " +
                    $"mínimo legal de {normative.MinWeeklyLectiveHours} h/semana.",
                    [
                        $"Aumenta SlotsPerDay o DaysPerWeek para alcanzar ≥ {normative.MinWeeklyLectiveHours} h lectivas/semana.",
                        "Por ejemplo: 5 tramos de 60 min × 5 días = 25 h/semana.",
                    ]));
            }
        }
    }

    private static void ValidateGroupHours(
        NormativeValidationData data, IStageNormative normative, List<ConflictExplanation> issues)
    {
        var byGroup = data.Assignments
            .GroupBy(d => d.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (groupId, assignments) in byGroup)
        {
            decimal totalH = assignments.Sum(a => (decimal)a.WeeklyHours);

            if (data.EnforceWeeklyLectiveMinimum && totalH < normative.MinWeeklyLectiveHours)
            {
                issues.Add(MakeIssue(ConflictSeverity.Error,
                    $"El grupo {GroupLabel(assignments)} solo tiene {totalH:F0} h/semana asignadas, " +
                    $"por debajo del mínimo legal de {normative.MinWeeklyLectiveHours} h.",
                    [
                        $"Añade asignaciones de asignaturas para completar al menos {normative.MinWeeklyLectiveHours} h lectivas/semana.",
                        "Consulta la tabla normativa de la etapa para los mínimos por área.",
                    ],
                    groupId: groupId));
            }

            int ingHours = assignments
                .Where(a => a.SubjectKey == "ing")
                .Sum(a => a.WeeklyHours);
            string modality = ingHours >= 5 ? "bilingue" : "estandar";
            var norms = normative.GetSubjects(modality);

            foreach (var norm in norms.Where(n => n.MinH > 0))
            {
                int assignedH = assignments
                    .Where(a => a.SubjectKey == norm.SubjectKey)
                    .Sum(a => a.WeeklyHours);

                if (assignedH < norm.MinH)
                {
                    issues.Add(MakeIssue(ConflictSeverity.Warning,
                        $"El grupo {GroupLabel(assignments)} tiene {assignedH} h/semana de {norm.SubjectName}, " +
                        $"por debajo del mínimo recomendado de {norm.MinH} h.",
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
                        $"superando el máximo de {norm.MaxH} h.",
                        [
                            $"Reduce las horas de {norm.SubjectName} a ≤ {norm.MaxH} h/semana.",
                        ],
                        groupId: groupId));
                }
            }
        }
    }

    private static string GroupLabel(List<NormativeAssignmentData> assignments)
    {
        return assignments.Count > 0
            ? assignments[0].GroupId.ToString()[..8] + "…"
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
