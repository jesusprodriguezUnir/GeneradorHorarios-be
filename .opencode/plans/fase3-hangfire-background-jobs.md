# Fase 3 — Hangfire Background Jobs para Generación de Horarios

## Contexto

Tras la Fase 1 (extracción del God Handler → Orchestrator) y la Fase 2 (repositories por aggregate), el endpoint `POST /api/schedules/generate` sigue ejecutando el motor de backtracking **sincrónicamente** en el hilo de request HTTP. Esto:

- Bloquea conexiones del pool durante segundos (hasta 30s timeout).
- Pierde progreso si el cliente cierra la conexión.
- No permite encolar múltiples generaciones ni reintentar fallos.

## Objetivo

Mover la ejecución del `IScheduleGenerationOrchestrator.GenerateAsync` a un **job en background** gestionado por Hangfire, con cola SQL Server y reporte de progreso vía SignalR directamente desde el job.

## Decisiones de diseño

1. **Hangfire como infraestructura**: los paquetes viven en `Infrastructure.csproj`; la API solo consume `IBackgroundJobClient` (abstracción de Hangfire).
2. **Progreso SignalR inline**: el endpoint ya no espera el resultado ni envía progreso. El job recibe el `schoolId` como grupo SignalR y emite `GenerationProgress` directamente desde su hilo de ejecución. No pasa por MediatR.
3. **El Orchestrator no cambia**: sigue aceptando `IProgress<GenerationProgress>?` y devolviendo `GenerateScheduleResult`. El job lo envuelve.
4. **Resultado final por SignalR**: al completar el job, emite un evento `GenerationCompleted` con el `scheduleId`, estado y metadatos, para que el frontend redirija a `/horarios/{id}`.
5. **Seguimiento REST opcional**: endpoint `GET /api/schedules/jobs/{jobId}` para consultar estado del job sin depender del dashboard de Hangfire.

---

## Paso 1 — Instalar paquetes Hangfire

Añadir a `src/HorariosEscolares.Infrastructure/HorariosEscolares.Infrastructure.csproj`:

```xml
<PackageReference Include="Hangfire.Core" Version="1.8.14" />
<PackageReference Include="Hangfire.SqlServer" Version="1.8.14" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.14" />
```

> Nota: `Hangfire.AspNetCore` es solo un metapaquete que referencia Core + Dashboard; como tenemos `AddHangfireServer` en la API, basta con referenciar Core + SqlServer en Infrastructure y AspNetCore en la API. Lo más limpio es añadir los tres en Infrastructure y que la API los herede por ProjectReference.

---

## Paso 2 — Configurar Hangfire en Program.cs

En `apps/api/Program.cs`:

```csharp
// Después de AddInfrastructure()
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // 2 workers para no saturar CPU en backtracking
    options.Queues = new[] { "default", "schedules" };
});
```

En el pipeline:

```csharp
if (!app.Environment.IsProduction())
{
    app.UseHangfireDashboard("/hangfire");
}
```

---

## Paso 3 — Crear el Background Job

**Archivo**: `src/HorariosEscolares.Infrastructure/BackgroundJobs/ScheduleGenerationJob.cs`

```csharp
public sealed class ScheduleGenerationJob(
    IScheduleGenerationOrchestrator orchestrator,
    IHubContext<GenerationProgressHub> hub,
    ILogger<ScheduleGenerationJob> logger)
{
    [Queue("schedules")]
    [AutomaticRetry(Attempts = 0)] // no reintentamos; el usuario puede lanzar de nuevo
    public async Task ExecuteAsync(
        Guid schoolId,
        string academicYear,
        int timeoutSeconds,
        string signalRGroup,
        CancellationToken ct)
    {
        var progress = new Progress<GenerationProgress>(async p =>
        {
            try
            {
                await hub.Clients.Group(signalRGroup)
                    .SendAsync("Progress", new
                    {
                        assigned = p.Assigned,
                        total = p.Total,
                        percentage = p.Percentage,
                        currentAction = p.CurrentAction,
                    }, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send SignalR progress.");
            }
        });

        try
        {
            var result = await orchestrator.GenerateAsync(
                schoolId, academicYear, timeoutSeconds, progress, ct);

            var payload = result switch
            {
                GenerateScheduleResult.Success s => new
                {
                    scheduleId = s.ScheduleId,
                    status = s.Status,
                    totalAssigned = s.TotalAssigned,
                    totalRequired = s.TotalRequired,
                    elapsedSeconds = s.ElapsedSeconds,
                    totalConflicts = s.TotalConflicts,
                    totalCost = s.TotalCost,
                },
                GenerateScheduleResult.ViabilityFailed f => new
                {
                    scheduleId = f.ScheduleId,
                    status = "failed",
                    totalAssigned = 0,
                    totalRequired = 0,
                    elapsedSeconds = 0,
                    totalConflicts = f.TotalConflicts,
                    conflicts = f.Conflicts.Select(c => new
                    {
                        type = c.Type.ToString().ToLower(),
                        severity = c.Severity.ToString().ToLower(),
                        description = c.Description,
                        suggestions = c.Suggestions,
                        teacherId = c.TeacherId,
                        groupId = c.GroupId,
                    }),
                },
                GenerateScheduleResult.NoAssignments => new { status = "no_assignments" },
                _ => new { status = "unknown" }
            };

            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", payload, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Schedule generation cancelled for school {SchoolId}.", schoolId);
            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", new { status = "cancelled" }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Schedule generation failed for school {SchoolId}.", schoolId);
            await hub.Clients.Group(signalRGroup)
                .SendAsync("GenerationCompleted", new { status = "error", message = ex.Message }, ct);
            throw; // Hangfire marcará el job como Failed
        }
    }
}
```

---

## Paso 4 — Refactorizar el endpoint POST /generate

En `apps/api/Features/Schedules/SchedulesFeature.cs`:

**Antes**: el endpoint ejecutaba `orchestrator.GenerateAsync` inline, bloqueaba el request y devolvía 200/400 con el resultado.

**Después**:

```csharp
g.MapPost("/generate", (
    HttpContext ctx,
    IBackgroundJobClient backgroundJobs,
    GenerateRequest req) =>
{
    var user = ctx.GetCurrentUserOrFail();
    if (!user.IsAdmin) return Results.StatusCode(403);

    var jobId = backgroundJobs.Enqueue<ScheduleGenerationJob>(job =>
        job.ExecuteAsync(
            user.SchoolId,
            req.AcademicYear,
            req.TimeoutSeconds,
            user.SchoolId.ToString(),
            CancellationToken.None)); // Hangfire gestiona su propio token

    return Results.Accepted($"/api/schedules/jobs/{jobId}", new { jobId });
});
```

> Nota: `CancellationToken.None` en `Enqueue` porque Hangfire serializa la expresión; el token real lo proporciona el job server al ejecutar.

---

## Paso 5 — Endpoint de seguimiento de jobs

Nuevo endpoint:

```csharp
g.MapGet("/jobs/{jobId}", (string jobId, IMonitoringApi monitor) =>
{
    var job = monitor.JobDetails(jobId);
    if (job is null) return Results.NotFound();

    return Results.Ok(new
    {
        id = jobId,
        state = job.History.OrderByDescending(h => h.CreatedAt).FirstOrDefault()?.StateName ?? "Unknown",
        createdAt = job.CreatedAt,
    });
});
```

Requiere registrar `IMonitoringApi` como singleton o resolver desde `JobStorage.Current.GetMonitoringApi()`.

---

## Paso 6 — Tests

### Integration tests

1. **Generate_Returns202_WithJobId**: `POST /generate` devuelve 202 y un `jobId` válido.
2. **Generate_JobExecutesOrchestrator**: usa `JobStorage.Current` + `IBackgroundJobClient.Enqueue` con un `FakeScheduleGenerationOrchestrator` inyectado vía test server para verificar que el job ejecuta el orchestrator.
3. **Generate_JobSendsSignalRProgress**: verificar que el job emite eventos `Progress` y `GenerationCompleted`.

### Unit tests (opcional)

- `ScheduleGenerationJobTests`: mockear `IScheduleGenerationOrchestrator` y `IHubContext<GenerationProgressHub>`, verificar que `SendAsync` se llama con `GenerationCompleted` al finalizar.

---

## Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `src/HorariosEscolares.Infrastructure/HorariosEscolares.Infrastructure.csproj` | +3 paquetes Hangfire |
| `src/HorariosEscolares.Infrastructure/DependencyInjection.cs` | + `services.AddHangfire(...)`? No, va en Program.cs de la API. Quizás nada. |
| `apps/api/Program.cs` | + `AddHangfire`, `AddHangfireServer`, `UseHangfireDashboard` |
| `apps/api/Features/Schedules/SchedulesFeature.cs` | Refactor `POST /generate`, añadir `GET /jobs/{jobId}` |
| `apps/api/api.csproj` | Hereda Hangfire por ProjectReference; no cambia. |
| `apps/api/appsettings.json` | Añadir config Hangfire opcional |
| `src/HorariosEscolares.Infrastructure/BackgroundJobs/ScheduleGenerationJob.cs` | **Nuevo** |
| `tests/api/Lectivo.IntegrationTests/SchedulesGenerationTests.cs` | + tests 202 + job execution |

---

## Riesgos / Consideraciones

1. **Hangfire crea tablas propias**: al arrancar la API, Hangfire.SqlServer creará automáticamente las tablas (`HangFire.*`) si no existen. Esto es seguro; no requiere migración EF.
2. **CancellationToken en background**: el token de `ExecuteAsync` es gestionado por Hangfire; si el servidor se apaga, el job se aborta y queda en estado Failed. El endpoint ya no tiene control sobre esto.
3. **Idempotencia**: si un admin hace clic dos veces, se encolan dos jobs. Esto es correcto; el frontend puede deshabilitar el botón.
4. **SignalR group**: el job usa `schoolId.ToString()` como grupo, igual que antes. Los clientes deben estar suscritos a ese grupo vía `JoinSchoolGroup`.

---

## Criterios de aceptación

- [ ] `POST /api/schedules/generate` devuelve `202 Accepted` con `jobId` inmediatamente (< 50 ms).
- [ ] El job se ejecuta en background y el motor de backtracking consume CPU en el worker, no en el request thread.
- [ ] Los clientes SignalR reciben eventos `Progress` y `GenerationCompleted` exactamente igual que antes.
- [ ] El endpoint `GET /api/schedules/jobs/{jobId}` devuelve estado del job.
- [ ] Dashboard Hangfire disponible en `/hangfire` (dev).
- [ ] 113/113 unit tests pasan, 33/33 integration tests pasan (o +2 nuevos).
