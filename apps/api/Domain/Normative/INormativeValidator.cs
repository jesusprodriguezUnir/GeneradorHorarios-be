using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Persistence.Entities;

namespace HorariosEscolares.Domain.Normative;

/// <summary>
/// Verifica que la configuración del centro y la distribución horaria de sus grupos
/// cumplen la normativa legal de Madrid (Decreto 61/2022 + Decreto 94/2025).
/// El validador AVISA pero no bloquea: los resultados se adjuntan como conflictos
/// de tipo Normativo en el horario generado.
/// </summary>
public interface INormativeValidator
{
    /// <summary>
    /// Valida el centro y sus asignaciones.
    /// </summary>
    /// <param name="school">Configuración del colegio.</param>
    /// <param name="assignmentData">
    /// Pares (Assignment, SubjectAllocation) de todas las asignaciones del colegio.
    /// </param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// Lista de incidencias normativas. Vacía si todo cumple la ley.
    /// Los elementos usan <see cref="ConflictExplanation"/> para reutilizar el
    /// modelo de conflictos ya existente en el motor de generación.
    /// </returns>
    Task<List<ConflictExplanation>> ValidateAsync(
        School school,
        IReadOnlyList<(Assignment Assignment, SubjectAllocation Allocation)> assignmentData,
        CancellationToken ct = default);
}
