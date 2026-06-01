using HorariosEscolares.Infrastructure.Persistence;

namespace HorariosEscolares.Features.Dev;

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Cuerpo del endpoint POST /api/dev/reseed.
/// Todos los campos son opcionales; los valores no enviados usan el default.
/// </summary>
public record ReseedRequest(
    int?    Levels         = null,
    int?    LinesPerLevel  = null,
    string? Modality       = null,
    string? ScheduleType   = null);

// ── Endpoints ─────────────────────────────────────────────────────────────────

/// <summary>
/// Endpoints solo disponibles en entorno de desarrollo (app.Environment.IsDevelopment()).
/// Registrados desde Program.cs dentro del bloque de IsDevelopment.
/// </summary>
public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dev").WithTags("Dev");

        // POST /api/dev/reseed
        // Vacía todos los datos del colegio demo y vuelve a sembrarlos con la
        // configuración indicada. Útil para cambiar modalidad (estándar/bilingüe)
        // o tipo de jornada (continua/partida) sin recrear la base de datos.
        //
        // Ejemplos:
        //   {}                                           → reseed con opciones por defecto
        //   { "modality": "bilingue" }                  → 6 cursos × 3 líneas bilingüe
        //   { "scheduleType": "partida" }               → jornada partida
        //   { "levels": 3, "linesPerLevel": 2,
        //     "modality": "bilingue",
        //     "scheduleType": "partida" }               → 3 cursos × 2 líneas bilingüe partida
        g.MapPost("/reseed", async (AppDbContext db, ReseedRequest? req) =>
        {
            var opts = new SeedOptions
            {
                Levels        = req?.Levels        ?? 6,
                LinesPerLevel = req?.LinesPerLevel  ?? 3,
                Modality      = req?.Modality       ?? "estandar",
                ScheduleType  = req?.ScheduleType   ?? "continua",
            };

            await DbInitializer.ReseedAsync(db, opts);

            return Results.Ok(new
            {
                message       = "Reseed completado.",
                levels        = opts.Levels,
                linesPerLevel = opts.LinesPerLevel,
                totalGroups   = opts.Levels * opts.LinesPerLevel,
                modality      = opts.Modality,
                scheduleType  = opts.ScheduleType,
            });
        });

        return app;
    }
}
