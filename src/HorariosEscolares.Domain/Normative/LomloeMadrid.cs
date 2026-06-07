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

    public static IReadOnlyList<SubjectNorm> GetSubjects(string modality)
    {
        var isBilingue = modality.Equals("bilingue", StringComparison.OrdinalIgnoreCase);
        var list = new List<SubjectNorm>();
        
        for (int cycle = 1; cycle <= 3; cycle++)
        {
            list.Add(new("len", "Lengua Castellana y Literatura", MinH: 4, MaxH: 6, DefaultH: 5, Cycle: cycle));
            list.Add(new("mat", "Matemáticas", MinH: 4, MaxH: 6, DefaultH: 5, Cycle: cycle));
            list.Add(new("cie", "Conocimiento del Medio Natural, Social y Cultural", MinH: 3, MaxH: 4, DefaultH: 3, Cycle: cycle));
            list.Add(new("ing", "Primera Lengua Extranjera (Inglés)", MinH: isBilingue ? 4 : 3, MaxH: isBilingue ? 6 : 5, DefaultH: isBilingue ? 5 : 4, RequiresSpecialist: true, Cycle: cycle));
            list.Add(new("ef",  "Educación Física", MinH: 2, MaxH: 3, DefaultH: 3, RequiresSpecialist: true, RequiredClassroomType: "gym", MaxConsecutiveSlots: 1, Cycle: cycle));
            list.Add(new("mus", "Educación Artística — Música", MinH: 1, MaxH: 2, DefaultH: 1, RequiresSpecialist: true, RequiredClassroomType: "music", MaxConsecutiveSlots: 1, Splittable: false, Cycle: cycle));
            list.Add(new("art", "Educación Artística — Plástica y Visual", MinH: 1, MaxH: 2, DefaultH: 2, Cycle: cycle));
            list.Add(new("rel", "Religión / Atención Educativa", MinH: 1, MaxH: 2, DefaultH: 1, MaxConsecutiveSlots: 1, Splittable: false, Cycle: cycle));
            
            if (cycle == 3)
            {
                list.Add(new("val", "Educación en Valores Cívicos y Éticos", MinH: 1, MaxH: 2, DefaultH: 1, MaxConsecutiveSlots: 1, Splittable: false, Cycle: cycle));
            }
            if (!isBilingue)
            {
                list.Add(new("tut", "Libre Configuración del Centro", MinH: 0, MaxH: 2, DefaultH: 1, Cycle: cycle));
            }
        }
        return list;
    }

    public static int DefaultWeeklyHours(string modality) => 25; // approximated or we can compute by cycle 1. We just return a constant since it varies by cycle now.

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
    bool Splittable = true,
    int? Cycle = null,
    int? CourseLevel = null);
