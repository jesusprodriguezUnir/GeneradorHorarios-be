namespace HorariosEscolares.Domain.Normative;

/// <summary>
/// Constantes legales y reglas de la normativa de Educación Primaria en la Comunidad de Madrid.
///
/// Fuentes:
///   - Decreto 61/2022, de 13 de julio, del Consejo de Gobierno de la Comunidad de Madrid
///     (BOCM 14 de julio de 2022) — establece la ordenación y el currículo de la etapa
///     de Educación Primaria (https://gestiona.comunidad.madrid/wleg_pub/).
///   - Decreto 94/2025, de 23 de diciembre (BOCM 26 de diciembre de 2025) — regula la
///     jornada escolar en centros de Infantil y Primaria de la Comunidad de Madrid.
///   - Orden 130/2023, de 23 de enero — normas de organización y funcionamiento de los
///     centros de Educación Primaria (BOCM 30 de enero de 2023).
///
/// Cada colegio tiene autonomía para distribuir horas dentro de los márgenes establecidos,
/// respetando siempre los mínimos legales.
/// </summary>
public static class LomloeMadrid
{
    // ── Jornada lectiva ────────────────────────────────────────────────────────

    /// <summary>
    /// Mínimo de horas lectivas semanales (sin incluir el recreo).
    /// Decreto 61/2022, Art. 10 — "22,5 horas semanales dedicadas al conjunto de las áreas".
    /// </summary>
    public const decimal MinWeeklyLectiveHours = 22.5m;

    /// <summary>Duración mínima del recreo diario, en minutos (30 min/día).</summary>
    public const int MinDailyBreakMinutes = 30;

    /// <summary>
    /// Horas semanales de recreo (30 min × 5 días = 2,5 h).
    /// Decreto 61/2022 — "mínimo de 2,5 horas repartidas en períodos diarios de recreo".
    /// </summary>
    public const decimal MinWeeklyBreakHours = 2.5m;

    /// <summary>Presencia total mínima semanal (lectivo + recreo) = 25 horas.</summary>
    public const decimal MinTotalWeeklyHours = MinWeeklyLectiveHours + MinWeeklyBreakHours;

    // ── Etapa educativa ────────────────────────────────────────────────────────

    public const int PrimariaMinLevel = 1;
    public const int PrimariaMaxLevel = 6;
    public const string Stage = "primaria";
    public const string Community = "madrid";

    // ── Distribución horaria por área (Decreto 61/2022, Anexo IV) ─────────────
    //
    // Los centros tienen autonomía para ajustar dentro de los márgenes (Min-Max),
    // siempre que el total de horas lectivas ≥ 22,5 h/semana y cada área cumpla su
    // mínimo. La Comunidad de Madrid puede establecer horas adicionales a los mínimos
    // estatales. Los valores de esta tabla reflejan la normativa de la CAM para primaria.

    /// <summary>
    /// Normas por área para la modalidad ESTÁNDAR (sin sección bilingüe).
    /// Horas semanales por grupo, aplicables a todos los niveles de primaria (1º-6º).
    /// </summary>
    public static readonly IReadOnlyList<SubjectNorm> StandardSubjects =
    [
        new("len", "Lengua Castellana y Literatura",                MinH: 4, MaxH: 6, DefaultH: 5),
        new("mat", "Matemáticas",                                   MinH: 4, MaxH: 6, DefaultH: 5),
        new("cie", "Conocimiento del Medio Natural, Social y Cultural", MinH: 3, MaxH: 4, DefaultH: 3),
        new("ing", "Primera Lengua Extranjera (Inglés)",            MinH: 3, MaxH: 5, DefaultH: 4,
            RequiresSpecialist: true),
        new("ef",  "Educación Física",                              MinH: 2, MaxH: 3, DefaultH: 3,
            RequiresSpecialist: true, RequiredClassroomType: "gym",
            MaxConsecutiveSlots: 1),
        new("mus", "Educación Artística — Música",                  MinH: 1, MaxH: 2, DefaultH: 1,
            RequiresSpecialist: true, RequiredClassroomType: "music",
            MaxConsecutiveSlots: 1, Splittable: false),
        new("art", "Educación Artística — Plástica y Visual",       MinH: 1, MaxH: 2, DefaultH: 2),
        new("rel", "Religión / Valores Sociales y Cívicos",         MinH: 1, MaxH: 2, DefaultH: 1,
            MaxConsecutiveSlots: 1, Splittable: false),
        new("tut", "Libre Configuración del Centro",                MinH: 0, MaxH: 2, DefaultH: 1),
    ];

    /// <summary>
    /// Normas por área para la modalidad BILINGÜE (sección en inglés).
    /// La primera lengua extranjera sube a 5 h/semana; se elimina la Libre Configuración
    /// para no superar la capacidad máxima de la rejilla horaria (25 h = 5 slots × 5 días).
    /// </summary>
    public static readonly IReadOnlyList<SubjectNorm> BilingueSubjects =
    [
        new("len", "Lengua Castellana y Literatura",                MinH: 4, MaxH: 6, DefaultH: 5),
        new("mat", "Matemáticas",                                   MinH: 4, MaxH: 6, DefaultH: 5),
        new("cie", "Conocimiento del Medio Natural, Social y Cultural", MinH: 3, MaxH: 4, DefaultH: 3),
        new("ing", "Primera Lengua Extranjera (Inglés)",            MinH: 4, MaxH: 6, DefaultH: 5,
            RequiresSpecialist: true),
        new("ef",  "Educación Física",                              MinH: 2, MaxH: 3, DefaultH: 3,
            RequiresSpecialist: true, RequiredClassroomType: "gym",
            MaxConsecutiveSlots: 1),
        new("mus", "Educación Artística — Música",                  MinH: 1, MaxH: 2, DefaultH: 1,
            RequiresSpecialist: true, RequiredClassroomType: "music",
            MaxConsecutiveSlots: 1, Splittable: false),
        new("art", "Educación Artística — Plástica y Visual",       MinH: 1, MaxH: 2, DefaultH: 2),
        new("rel", "Religión / Valores Sociales y Cívicos",         MinH: 1, MaxH: 2, DefaultH: 1,
            MaxConsecutiveSlots: 1, Splittable: false),
    ];

    /// <summary>Devuelve las normas de asignaturas para la modalidad indicada.</summary>
    public static IReadOnlyList<SubjectNorm> GetSubjects(string modality) =>
        modality.Equals("bilingue", StringComparison.OrdinalIgnoreCase)
            ? BilingueSubjects
            : StandardSubjects;

    /// <summary>
    /// Suma de horas lectivas semanales por defecto (sin contar el recreo).
    /// Estándar: 5+5+3+4+3+1+2+1+1 = 25 h (1 h sobre el mínimo legal, dentro de la capacidad).
    /// Bilingüe: 5+5+3+5+3+1+2+1 = 25 h (exactamente la capacidad de la rejilla 5×5).
    /// </summary>
    public static int DefaultWeeklyHours(string modality) =>
        GetSubjects(modality).Sum(s => s.DefaultH);

    /// <summary>
    /// Comprueba si un total de horas lectivas semanales cumple el mínimo legal (22,5 h).
    /// </summary>
    public static bool MeetsLectiveMinimum(decimal weeklyHours) =>
        weeklyHours >= MinWeeklyLectiveHours;
}

/// <summary>
/// Norma horaria de un área según el Decreto 61/2022 de Madrid.
/// </summary>
/// <param name="SubjectKey">Clave interna del área (len, mat, ing, ef, mus, art, rel, tut).</param>
/// <param name="SubjectName">Nombre oficial del área.</param>
/// <param name="MinH">Mínimo de horas semanales (límite legal inferior).</param>
/// <param name="MaxH">Máximo de horas semanales (límite legal superior).</param>
/// <param name="DefaultH">Horas semanales recomendadas para el colegio demo.</param>
/// <param name="RequiresSpecialist">Si el área exige un especialista (Inglés, EF, Música).</param>
/// <param name="RequiredClassroomType">Tipo de aula requerida, si aplica (gym, music).</param>
/// <param name="MaxConsecutiveSlots">Máximo de sesiones consecutivas del área (def. 2).</param>
/// <param name="Splittable">Si el área puede repartirse en días distintos (def. true).</param>
public record SubjectNorm(
    string SubjectKey,
    string SubjectName,
    int MinH,
    int MaxH,
    int DefaultH,
    bool RequiresSpecialist = false,
    string? RequiredClassroomType = null,
    int MaxConsecutiveSlots = 2,
    bool Splittable = true);
