using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Normative;

/// <summary>
/// Normativa LOMLOE de una etapa educativa concreta: niveles permitidos, carga
/// lectiva mínima, recreo mínimo y distribución de áreas por modalidad.
/// Punto de extensión para añadir Infantil (Decreto 36/2022) y ESO (Decreto 65/2022).
/// </summary>
public interface IStageNormative
{
    string StageType { get; }
    int MinLevel { get; }
    int MaxLevel { get; }
    decimal MinWeeklyLectiveHours { get; }
    int MinDailyBreakMinutes { get; }
    IReadOnlyList<SubjectNorm> GetSubjects(string modality);
}

/// <summary>Educación Primaria — Decreto 61/2022 (Comunidad de Madrid).</summary>
public sealed class PrimariaNormative : IStageNormative
{
    public string StageType => StageTypes.Primaria;
    public int MinLevel => LomloeMadrid.PrimariaMinLevel;
    public int MaxLevel => LomloeMadrid.PrimariaMaxLevel;
    public decimal MinWeeklyLectiveHours => LomloeMadrid.MinWeeklyLectiveHours;
    public int MinDailyBreakMinutes => LomloeMadrid.MinDailyBreakMinutes;
    public IReadOnlyList<SubjectNorm> GetSubjects(string modality) => LomloeMadrid.GetSubjects(modality);
}

/// <summary>
/// Educación Infantil (2º ciclo, 3-6 años) — Decreto 36/2022 (Comunidad de Madrid).
/// Esqueleto: niveles y mínimos provisionales; las áreas se completarán al
/// incorporar la tabla oficial del decreto.
/// </summary>
public sealed class InfantilNormative : IStageNormative
{
    public string StageType => StageTypes.Infantil;
    public int MinLevel => 1;
    public int MaxLevel => 3;
    public decimal MinWeeklyLectiveHours => 22.5m;       // TODO: confirmar con Decreto 36/2022
    public int MinDailyBreakMinutes => 30;
    public IReadOnlyList<SubjectNorm> GetSubjects(string modality) => [];   // TODO: áreas de Infantil
}

/// <summary>
/// Educación Secundaria Obligatoria (ESO, 1º-4º) — Decreto 65/2022 (Comunidad de Madrid).
/// Esqueleto: niveles y mínimos provisionales; las materias se completarán al
/// incorporar la tabla oficial del decreto.
/// </summary>
public sealed class SecundariaNormative : IStageNormative
{
    public string StageType => StageTypes.Secundaria;
    public int MinLevel => 1;
    public int MaxLevel => 4;
    public decimal MinWeeklyLectiveHours => 30m;         // TODO: confirmar con Decreto 65/2022
    public int MinDailyBreakMinutes => 30;
    public IReadOnlyList<SubjectNorm> GetSubjects(string modality) => [];   // TODO: materias de ESO
}

/// <summary>Resuelve la normativa aplicable a partir del tipo de etapa.</summary>
public interface INormativeStageRegistry
{
    IStageNormative? Resolve(string stageType);
}

public sealed class NormativeStageRegistry : INormativeStageRegistry
{
    private readonly Dictionary<string, IStageNormative> _byType;

    public NormativeStageRegistry(IEnumerable<IStageNormative>? normatives = null)
    {
        var list = normatives?.ToList();
        if (list is null || list.Count == 0)
            list = DefaultNormatives();

        _byType = list.ToDictionary(n => n.StageType, StringComparer.OrdinalIgnoreCase);
    }

    private static List<IStageNormative> DefaultNormatives() =>
        [new PrimariaNormative(), new InfantilNormative(), new SecundariaNormative()];

    public IStageNormative? Resolve(string stageType) =>
        _byType.GetValueOrDefault(stageType ?? string.Empty);
}
