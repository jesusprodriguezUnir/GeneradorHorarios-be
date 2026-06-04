using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Services;

/// <summary>
/// Resuelve a qué ciclo pertenece un curso dentro de una etapa educativa.
/// La agrupación en ciclos depende de la etapa, por lo que la regla vive
/// centralizada aquí en lugar de en la entidad <see cref="CourseGroup"/>.
/// </summary>
public interface ICycleResolver
{
    int ResolveCycle(string stageType, int courseLevel);
}

public sealed class CycleResolver : ICycleResolver
{
    public int ResolveCycle(string stageType, int courseLevel) =>
        (stageType ?? string.Empty).ToLowerInvariant() switch
        {
            // Infantil (2º ciclo LOMLOE, 3-6 años): ciclo único.
            StageTypes.Infantil => 1,
            // Secundaria/ESO: agrupación provisional 1º-3º / 4º.
            // TODO: afinar con el Decreto 65/2022 (ordenación de la ESO en Madrid).
            StageTypes.Secundaria => courseLevel <= 3 ? 1 : 2,
            // Primaria (Decreto 61/2022): 1º-2º → 1, 3º-4º → 2, 5º-6º → 3.
            _ => (courseLevel + 1) / 2,
        };
}
