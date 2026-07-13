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
/// Educación Infantil (1er ciclo, 0-3 años) — Decreto 36/2022 (Comunidad de Madrid).
/// </summary>
public sealed class InfantilNormative : IStageNormative
{
    public string StageType => StageTypes.Infantil;
    public int MinLevel => 1;
    public int MaxLevel => 2;
    public decimal MinWeeklyLectiveHours => 22.5m;
    public int MinDailyBreakMinutes => 30;
    public IReadOnlyList<SubjectNorm> GetSubjects(string modality)
    {
        var list = new List<SubjectNorm>();
        for (int cycle = 1; cycle <= 2; cycle++)
        {
            list.Add(new("crec", "Crecimiento en Armonía",                     MinH: 5, MaxH: 10, DefaultH: 8, Cycle: cycle));
            list.Add(new("desc", "Descubrimiento y Exploración del Entorno",    MinH: 4, MaxH: 9, DefaultH: 7, Cycle: cycle));
            list.Add(new("com",  "Comunicación y Representación de la Realidad", MinH: 5, MaxH: 10, DefaultH: 8, Cycle: cycle));
        }
        return list;
    }
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
    public decimal MinWeeklyLectiveHours => 30m;         // Confirmado con Decreto 65/2022: 30h semanales lectivas
    public int MinDailyBreakMinutes => 30;
    public IReadOnlyList<SubjectNorm> GetSubjects(string modality)
    {
        var isBilingue = modality.Equals("bilingue", StringComparison.OrdinalIgnoreCase);
        var list = new List<SubjectNorm>();
        for (int course = 1; course <= 4; course++)
        {
            // Materias comunes a todos los cursos de ESO
            list.Add(new("len", "Lengua Castellana y Literatura",             MinH: 4, MaxH: 6, DefaultH: course == 1 || course == 2 ? 5 : 4, CourseLevel: course));
            list.Add(new("mat", "Matemáticas",                                MinH: 3, MaxH: 5, DefaultH: 4, CourseLevel: course));
            list.Add(new("ing", "Primera Lengua Extranjera (Inglés)",         MinH: isBilingue ? 4 : 3, MaxH: isBilingue ? 6 : 5, DefaultH: isBilingue ? 5 : (course == 1 || course == 2 ? 4 : 4), RequiresSpecialist: true, CourseLevel: course));
            list.Add(new("gh",  "Geografía e Historia",                       MinH: 3, MaxH: 4, DefaultH: 3, CourseLevel: course));
            list.Add(new("ef",  "Educación Física",                           MinH: 2, MaxH: 3, DefaultH: 2, RequiresSpecialist: true, RequiredClassroomType: "gym", MaxConsecutiveSlots: 1, CourseLevel: course));
            list.Add(new("tut", "Tutoría",                                    MinH: 1, MaxH: 1, DefaultH: 1, MaxConsecutiveSlots: 1, Splittable: false, CourseLevel: course));
            list.Add(new("rel", "Religión o Atención Educativa",              MinH: 1, MaxH: 2, DefaultH: course == 1 ? 2 : 1, MaxConsecutiveSlots: 1, Splittable: false, CourseLevel: course));
            list.Add(new("opt", "Materias de Opción / Optativas",             MinH: 2, MaxH: 12, DefaultH: course == 4 ? 11 : 2, CourseLevel: course));

            // Materias específicas por nivel
            if (course == 1)
            {
                list.Add(new("bg",  "Biología y Geología",                        MinH: 2, MaxH: 3, DefaultH: 3, CourseLevel: course));
                list.Add(new("art", "Educación Plástica, Visual y Audiovisual",    MinH: 2, MaxH: 2, DefaultH: 2, CourseLevel: course));
                list.Add(new("mus", "Música",                                     MinH: 2, MaxH: 2, DefaultH: 2, RequiresSpecialist: true, RequiredClassroomType: "music", MaxConsecutiveSlots: 1, Splittable: false, CourseLevel: course));
            }
            else if (course == 2)
            {
                list.Add(new("fq",  "Física y Química",                           MinH: 2, MaxH: 3, DefaultH: 3, CourseLevel: course));
                list.Add(new("tec", "Tecnología y Digitalización",                MinH: 2, MaxH: 3, DefaultH: 3, CourseLevel: course));
                list.Add(new("art", "Educación Plástica, Visual y Audiovisual",    MinH: 2, MaxH: 2, DefaultH: 2, CourseLevel: course));
                list.Add(new("val", "Educación en Valores Cívicos y Éticos",      MinH: 1, MaxH: 2, DefaultH: 1, MaxConsecutiveSlots: 1, Splittable: false, CourseLevel: course));
                list.Add(new("mus", "Música",                                     MinH: 2, MaxH: 2, DefaultH: 2, RequiresSpecialist: true, RequiredClassroomType: "music", MaxConsecutiveSlots: 1, Splittable: false, CourseLevel: course));
            }
            else if (course == 3)
            {
                list.Add(new("bg",  "Biología y Geología",                        MinH: 2, MaxH: 3, DefaultH: 2, CourseLevel: course));
                list.Add(new("fq",  "Física y Química",                           MinH: 2, MaxH: 3, DefaultH: 2, CourseLevel: course));
                list.Add(new("tec", "Tecnología y Digitalización",                MinH: 2, MaxH: 3, DefaultH: 2, CourseLevel: course));
                list.Add(new("mus", "Música",                                     MinH: 2, MaxH: 2, DefaultH: 2, RequiresSpecialist: true, RequiredClassroomType: "music", MaxConsecutiveSlots: 1, Splittable: false, CourseLevel: course));
            }
            // En 4º ESO (course == 4), las materias adicionales van incluidas en "opt" (Materias de Opción) de 11h.
        }
        return list;
    }
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
