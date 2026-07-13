# Plan: Extracción del God Handler (POST /generate) — Fase 1

## Contexto

El endpoint `POST /api/schedules/generate` en `apps/api/Features/Schedules/SchedulesFeature.cs:59-311` contiene ~250 líneas de lógica de orquestación que debería estar en la capa Application. Incluye: carga de datos de 7+ tablas, construcción de sesiones/constraints, análisis de viabilidad, validación normativa, invocación del motor de backtracking, post-procesamiento de conflictos, persistencia de resultados y emisión de progreso via SignalR.

## Decisiones

- **SignalR**: Se mantiene inline. El endpoint crea `IProgress<GenerationProgress>` conectado al hub y lo pasa al orchestrator.
- **Repositories**: Solo para writes. Las reads siguen con `IAppDbContext` directamente.
- **Ejecución**: Fase por fase con revisión.

## Arquitectura Objetivo

```
POST /api/schedules/generate
  │
  ├─ Endpoint (~25 líneas): auth check → crea IProgress<T> → orchestrator.GenerateAsync()
  │
  └─ GenerateScheduleOrchestrator (Application layer)
       ├─ IAppDbContext (reads)
       ├─ ScheduleViabilityAnalyzer (Domain, movido desde Infrastructure)
       ├─ INormativeValidator
       ├─ IScheduleEngine
       ├─ IScheduleRepository (writes)
       └─ IProgress<GenerationProgress> (SignalR)
```

## Pasos de Implementación

### 1.1 Crear `IScheduleRepository` en Domain

**Nuevo archivo:** `src/HorariosEscolares.Domain/Scheduling/IScheduleRepository.cs`

```csharp
using HorariosEscolares.Domain.Entities;

namespace HorariosEscolares.Domain.Scheduling;

public interface IScheduleRepository
{
    Task AddScheduleWithDetailsAsync(
        ScheduleRecord schedule,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<ScheduleConflictRecord> conflicts,
        CancellationToken ct);
}
```

### 1.2 Mover `ScheduleViabilityAnalyzer` a Domain

- **Origen:** `src/HorariosEscolares.Infrastructure/Engine/ScheduleViabilityAnalyzer.cs`
- **Destino:** `src/HorariosEscolares.Domain/Services/ScheduleViabilityAnalyzer.cs`
- **Cambio:** Namespace de `HorariosEscolares.Infrastructure.Engine` a `HorariosEscolares.Domain.Services`
- **Eliminar** el archivo original en Infrastructure/Engine/
- **Actualizar usings** en:
  - `tests/api/Lectivo.UnitTests/ScheduleViabilityAnalyzerTests.cs` (cambiar `using HorariosEscolares.Infrastructure.Engine` por `using HorariosEscolares.Domain.Services`)

### 1.3 Crear Command + Result DTOs

**Nuevos archivos en** `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/`:

**`GenerateScheduleCommand.cs`:**
```csharp
public record GenerateScheduleCommand(
    string AcademicYear,
    int TimeoutSeconds = 30) : IRequest<GenerateScheduleResult>;
```

**`GenerateScheduleResult.cs`:**
```csharp
public abstract record GenerateScheduleResult
{
    public sealed record Success(
        Guid ScheduleId, string Status,
        int TotalAssigned, int TotalRequired,
        int ElapsedSeconds, int TotalConflicts, int TotalCost
    ) : GenerateScheduleResult;

    public sealed record ViabilityFailed(
        Guid ScheduleId, int TotalConflicts,
        IReadOnlyList<ConflictExplanation> Conflicts
    ) : GenerateScheduleResult;

    public sealed record NoAssignments() : GenerateScheduleResult;
}
```

### 1.4 Crear Orchestrator + Handler

**Nuevos archivos en** `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/`:

**`IScheduleGenerationOrchestrator.cs`:**
```csharp
public interface IScheduleGenerationOrchestrator
{
    Task<GenerateScheduleResult> GenerateAsync(
        Guid schoolId,
        string academicYear,
        int timeoutSeconds,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct);
}
```

**`GenerateScheduleOrchestrator.cs`** (~200 líneas):
- Extrae `BuildSessions()` como método privado
- Extrae `BuildHardConstraints()` como método privado
- Extrae `BuildSoftConstraints()` como método privado
- Extrae `DetectCoverageConflicts()` y `DetectTeacherHoursConflicts()` como métodos privados
- Extrae `ResolveClassroomForSlot()` como método privado
- Usa `IAppDbContext` para reads
- Usa `IScheduleRepository` para writes
- Acepta `IProgress<GenerationProgress>` para SignalR (no depende de ASP.NET Core)

**`GenerateScheduleHandler.cs`:**
```csharp
public sealed class GenerateScheduleHandler(
    ICurrentUser user,
    IScheduleGenerationOrchestrator orchestrator)
    : IRequestHandler<GenerateScheduleCommand, GenerateScheduleResult>
{
    public async Task<GenerateScheduleResult> Handle(GenerateScheduleCommand request, CancellationToken ct)
    {
        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Solo administradores pueden generar horarios.");
        return await orchestrator.GenerateAsync(
            user.SchoolId, request.AcademicYear, request.TimeoutSeconds, null, ct);
    }
}
```

**Nota:** El handler pasa `null` como progress. El endpoint llama directamente al orchestrator (no via MediatR) para poder inyectar el `IProgress<T>` conectado a SignalR.

### 1.5 Crear `ScheduleRepository`

**Nuevo archivo:** `src/HorariosEscolares.Infrastructure/Persistence/Repositories/ScheduleRepository.cs`

```csharp
public sealed class ScheduleRepository(AppDbContext db) : IScheduleRepository
{
    public async Task AddScheduleWithDetailsAsync(
        ScheduleRecord schedule,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<ScheduleConflictRecord> conflicts,
        CancellationToken ct)
    {
        db.Schedules.Add(schedule);
        db.ScheduleEntries.AddRange(entries);
        db.ScheduleConflicts.AddRange(conflicts);
        await db.SaveChangesAsync(ct);
    }
}
```

### 1.6 Actualizar DI

**`src/HorariosEscolares.Infrastructure/DependencyInjection.cs`:**
- Añadir: `services.AddScoped<IScheduleRepository, ScheduleRepository>();`

**`src/HorariosEscolares.Application/DependencyInjection.cs`:**
- Añadir: `services.AddScoped<IScheduleGenerationOrchestrator, GenerateScheduleOrchestrator>();`

### 1.7 Refactorizar endpoint

**`apps/api/Features/Schedules/SchedulesFeature.cs`:**
- Reducir `POST /generate` de ~250 líneas a ~25 líneas
- Endpoint inyecta `IScheduleGenerationOrchestrator` directamente (no via MediatR)
- Crea `IProgress<GenerationProgress>` conectado a SignalR hub
- Eliminar métodos privados: `BuildSessions`, `BuildHardConstraints`, `BuildSoftConstraints`
- Mover `ParseClassroomType` y `DayName` al orchestrator
- Mantener `GenerationProgressHub` en el mismo archivo

### 1.8 Tests del orchestrator

**Nuevo archivo:** `tests/api/Lectivo.UnitTests/GenerateScheduleOrchestratorTests.cs`

- Añadir `NSubstitute` al proyecto de tests unitarios
- Tests clave:
  1. School no encontrado → `NotFoundException`
  2. Sin asignaciones → `NoAssignments` result
  3. Viabilidad fallida → `ViabilityFailed` con conflictos
  4. Generación exitosa → `Success` con schedule persistido via repository
  5. Conflicts post-generación (coverage, teacher hours) se incluyen

### 1.9 Validación

```bash
dotnet build
dotnet test tests/api/Lectivo.UnitTests
dotnet test tests/api/Lectivo.IntegrationTests
```

## Archivos Afectados

| Acción | Archivo |
|--------|---------|
| NUEVO | `src/HorariosEscolares.Domain/Scheduling/IScheduleRepository.cs` |
| MOVER | `ScheduleViabilityAnalyzer.cs` de Infrastructure/Engine → Domain/Services |
| ELIMINAR | `src/HorariosEscolares.Infrastructure/Engine/ScheduleViabilityAnalyzer.cs` |
| NUEVO | `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/GenerateScheduleCommand.cs` |
| NUEVO | `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/GenerateScheduleResult.cs` |
| NUEVO | `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/IScheduleGenerationOrchestrator.cs` |
| NUEVO | `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/GenerateScheduleOrchestrator.cs` |
| NUEVO | `src/HorariosEscolares.Application/Features/Schedules/Commands/GenerateSchedule/GenerateScheduleHandler.cs` |
| NUEVO | `src/HorariosEscolares.Infrastructure/Persistence/Repositories/ScheduleRepository.cs` |
| MODIFICAR | `src/HorariosEscolares.Infrastructure/DependencyInjection.cs` |
| MODIFICAR | `src/HorariosEscolares.Application/DependencyInjection.cs` |
| MODIFICAR | `apps/api/Features/Schedules/SchedulesFeature.cs` |
| MODIFICAR | `tests/api/Lectivo.UnitTests/ScheduleViabilityAnalyzerTests.cs` |
| MODIFICAR | `tests/api/Lectivo.UnitTests/Lectivo.UnitTests.csproj` |
| NUEVO | `tests/api/Lectivo.UnitTests/GenerateScheduleOrchestratorTests.cs` |

## Verificación

1. `dotnet build` compila sin errores
2. `dotnet test tests/api/Lectivo.UnitTests` — todos los tests pasan (79+ existentes + nuevos)
3. `dotnet test tests/api/Lectivo.IntegrationTests` — todos los tests pasan (30+ existentes, sin regresiones)
4. El endpoint `POST /api/schedules/generate` funciona exactamente igual que antes (mismo request/response)
5. El `SchedulesFeature.cs` se reduce de ~458 líneas a ~230 líneas
