using HorariosEscolares.Domain.Entities;
using HorariosEscolares.Domain.Services;

namespace HorariosEscolares.Infrastructure.Engine;

/// <summary>
/// Analiza la viabilidad de una configuración de horario ANTES de ejecutar el motor,
/// detectando configuraciones imposibles de forma rápida (O(n), sin backtracking).
/// Equivale al "analizador de condiciones" de Penalara GHC / Horarium.
///
/// Comprobaciones que realiza:
///   1. Especialista ausente — profesor asignado sin la especialidad requerida.
///   2. Tipo de aula inexistente — se requiere un aula especial que el colegio no tiene.
///   3. Capacidad por grupo — un grupo tiene más sesiones asignadas que slots lectivos disponibles.
///   4. Demanda por profesor — un profesor tiene más sesiones que slots en los que puede impartir.
///   5. Capacidad de aulas especiales — el total de sesiones de un tipo supera
///      la oferta combinada de aulas × slots lectivos.
/// </summary>
public static class ScheduleViabilityAnalyzer
{
    /// <summary>
    /// Ejecuta el análisis de viabilidad.
    /// </summary>
    /// <param name="school">Configuración del centro (días, slots, aulas).</param>
    /// <param name="sessions">Sesiones que el motor debe asignar.</param>
    /// <param name="unavailableSlots">Slots en que cada profesor no está disponible.</param>
    /// <returns>
    /// Lista de <see cref="ConflictExplanation"/> con <c>Severity = Error</c>.
    /// Lista vacía si la configuración es potencialmente viable.
    /// </returns>
    public static IReadOnlyList<ConflictExplanation> Analyze(
        SchoolConfig school,
        IReadOnlyList<SessionToAssign> sessions,
        IReadOnlySet<(Guid TeacherId, int Day, int Slot)> unavailableSlots)
    {
        var conflicts = new List<ConflictExplanation>();

        int lectiveSlots = school.Slots.Count(s => !s.IsBreak);
        int totalLectiveSlots = school.WorkingDays.Count * lectiveSlots; // slots lectivos/semana

        // ── 1. Especialista ausente ───────────────────────────────────────────
        foreach (var session in sessions.Where(s => s.RequiresSpecialist))
        {
            if (!HasRequiredSpecialty(session))
            {
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Teacher,
                    Severity = ConflictSeverity.Error,
                    Description = $"El profesor asignado a '{session.SubjectName}' ({session.GroupLabel}) no tiene la especialidad requerida.",
                    Suggestions =
                    [
                        $"Asigna un profesor con la especialidad '{RequiredSpecialtyLabel(session.SubjectKey)}' a esta sesión.",
                        "Ve a Configuración → Profesores para revisar las especialidades registradas.",
                    ],
                    TeacherId = session.TeacherId,
                });
            }
        }

        // ── 2. Tipo de aula inexistente ───────────────────────────────────────
        var classroomTypesByType = school.Classrooms
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var requiredClassroomTypes = sessions
            .Where(s => s.RequiredClassroomType.HasValue)
            .GroupBy(s => s.RequiredClassroomType!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (requiredType, sessionsOfType) in requiredClassroomTypes)
        {
            if (!classroomTypesByType.ContainsKey(requiredType))
            {
                // No hay ni una sola aula de ese tipo → todas las sesiones son irresolubles
                var subjectNames = sessionsOfType.Select(s => s.SubjectName).Distinct().ToList();
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Classroom,
                    Severity = ConflictSeverity.Error,
                    Description = $"El centro no tiene ninguna aula de tipo '{requiredType}', requerida por: {string.Join(", ", subjectNames)}.",
                    Suggestions =
                    [
                        $"Ve a Configuración → Espacios y añade un aula de tipo '{requiredType}'.",
                        "Alternativamente, revisa si la asignatura realmente requiere ese tipo de espacio.",
                    ],
                });
            }
        }

        // ── 3. Capacidad por grupo ────────────────────────────────────────────
        var sessionsByGroup = sessions.GroupBy(s => s.GroupId);
        foreach (var group in sessionsByGroup)
        {
            int sessionCount = group.Count();
            if (sessionCount > totalLectiveSlots)
            {
                var first = group.First();
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Coverage,
                    Severity = ConflictSeverity.Error,
                    Description = $"El grupo '{first.GroupLabel}' tiene {sessionCount} sesiones asignadas, pero solo hay {totalLectiveSlots} slots lectivos por semana.",
                    Suggestions =
                    [
                        "Reduce el número total de horas lectivas asignadas al grupo.",
                        $"O amplía la jornada (actualmente {school.WorkingDays.Count} días × {lectiveSlots} slots = {totalLectiveSlots} huecos disponibles).",
                    ],
                    GroupId = group.Key,
                });
            }
        }

        // ── 4. Demanda por profesor ───────────────────────────────────────────
        var sessionsByTeacher = sessions.GroupBy(s => s.TeacherId);
        foreach (var teacherGroup in sessionsByTeacher)
        {
            var teacherId = teacherGroup.Key;
            int sessionCount = teacherGroup.Count();

            // Slots en los que el profesor no está disponible esta semana
            int unavailableCount = school.WorkingDays
                .SelectMany(day => school.Slots
                    .Where(s => !s.IsBreak)
                    .Select(s => (teacherId, day, s.Index)))
                .Count(t => unavailableSlots.Contains(t));

            int availableSlots = totalLectiveSlots - unavailableCount;

            // Límite adicional: máximo semanal declarado en la asignación
            int maxWeeklyHours = teacherGroup.First().TeacherMaxWeeklyHours;
            int effectiveLimit = Math.Min(availableSlots, maxWeeklyHours);

            if (sessionCount > effectiveLimit)
            {
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Teacher,
                    Severity = ConflictSeverity.Error,
                    Description = $"El profesor tiene {sessionCount} sesiones asignadas, pero solo puede impartir {effectiveLimit} ({availableSlots} slots disponibles, límite semanal {maxWeeklyHours}h).",
                    Suggestions =
                    [
                        "Redistribuye algunas sesiones a otro profesor.",
                        "Revisa las no-disponibilidades del profesor en Configuración → Profesores.",
                        "O aumenta el límite de horas semanales si la normativa lo permite.",
                    ],
                    TeacherId = teacherId,
                });
            }
        }

        // ── 5. Capacidad de aulas especiales ─────────────────────────────────
        foreach (var (requiredType, sessionsOfType) in requiredClassroomTypes)
        {
            if (!classroomTypesByType.TryGetValue(requiredType, out int classroomCount))
                continue; // ya reportado en check 2

            // Oferta máxima = aulas de ese tipo × slots lectivos totales
            int supply = classroomCount * totalLectiveSlots;
            int demand = sessionsOfType.Count;

            if (demand > supply)
            {
                var subjectNames = sessionsOfType.Select(s => s.SubjectName).Distinct().ToList();
                conflicts.Add(new ConflictExplanation
                {
                    Type = ConflictType.Classroom,
                    Severity = ConflictSeverity.Error,
                    Description = $"Hay {demand} sesiones que requieren aula '{requiredType}', pero solo hay capacidad para {supply} ({classroomCount} aula(s) × {totalLectiveSlots} slots). Afecta a: {string.Join(", ", subjectNames)}.",
                    Suggestions =
                    [
                        $"Añade más aulas de tipo '{requiredType}' en Configuración → Espacios.",
                        "O reduce el número de sesiones que requieren ese tipo de espacio.",
                    ],
                });
            }
        }

        return conflicts;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool HasRequiredSpecialty(SessionToAssign session)
    {
        var specialties = session.TeacherSpecialties;
        var key = session.SubjectKey.ToLower();

        return key switch
        {
            "ing" => specialties.Any(s => s.Contains("Inglés", StringComparison.OrdinalIgnoreCase)),
            "ef"  => specialties.Any(s => s.Contains("Física", StringComparison.OrdinalIgnoreCase)
                                       || s.Contains("Deporte", StringComparison.OrdinalIgnoreCase)),
            "mus" => specialties.Any(s => s.Contains("Música", StringComparison.OrdinalIgnoreCase)),
            _     => specialties.Any(s => s.Contains("Generalista", StringComparison.OrdinalIgnoreCase)),
        };
    }

    private static string RequiredSpecialtyLabel(string subjectKey) => subjectKey.ToLower() switch
    {
        "ing" => "Inglés",
        "ef"  => "Educación Física / Deporte",
        "mus" => "Música",
        _     => "Generalista",
    };
}
