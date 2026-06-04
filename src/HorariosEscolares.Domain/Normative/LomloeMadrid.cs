namespace HorariosEscolares.Domain.Normative;

public static class LomloeMadrid
{
    public const decimal MinWeeklyLectiveHours = 22.5m;
    public const int MinDailyBreakMinutes = 30;
    public const decimal MinWeeklyBreakHours = 2.5m;
    public const decimal MinTotalWeeklyHours = MinWeeklyLectiveHours + MinWeeklyBreakHours;
    public const int PrimariaMinLevel = 1;
    public const int PrimariaMaxLevel = 6;
    public const string Stage = "primaria";
    public const string Community = "madrid";

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

    public static IReadOnlyList<SubjectNorm> GetSubjects(string modality) =>
        modality.Equals("bilingue", StringComparison.OrdinalIgnoreCase)
            ? BilingueSubjects
            : StandardSubjects;

    public static int DefaultWeeklyHours(string modality) =>
        GetSubjects(modality).Sum(s => s.DefaultH);

    public static bool MeetsLectiveMinimum(decimal weeklyHours) =>
        weeklyHours >= MinWeeklyLectiveHours;
}

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
