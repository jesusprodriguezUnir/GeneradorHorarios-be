namespace HorariosEscolares.Infrastructure.Persistence;

/// <summary>
/// Opciones de configuración para el seed de datos de desarrollo.
/// Se leen de la sección "Seed" en appsettings.Development.json,
/// o se pasan directamente al endpoint POST /api/dev/reseed.
/// </summary>
public class SeedOptions
{
    /// <summary>
    /// Número de niveles de Educación Primaria (1–6).
    /// Default: 6 (colegio completo).
    /// </summary>
    public int Levels { get; set; } = 6;

    /// <summary>
    /// Número de líneas (grupos) por nivel. Letras: A, B, C…
    /// Default: 3 (colegio de tres líneas).
    /// </summary>
    public int LinesPerLevel { get; set; } = 3;

    /// <summary>
    /// Modalidad lingüística: "estandar" | "bilingue".
    /// Afecta las horas de Inglés (4 h vs 5 h) y el número de especialistas.
    /// Default: "estandar".
    /// </summary>
    public string Modality { get; set; } = "estandar";

    /// <summary>
    /// Tipo de jornada: "continua" | "partida".
    /// Default: "continua" (jornada de mañana, la más habitual en Madrid).
    /// </summary>
    public string ScheduleType { get; set; } = "continua";
}
