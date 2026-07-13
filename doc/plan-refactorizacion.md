# Plan de refactorización — GeneradorHorarios-be (Lectivo)

## Contexto

El proyecto tiene una arquitectura sólida en capas (Domain/Application/Infrastructure + `apps/api`)
con CQRS vía MediatR, FluentValidation, Hangfire y SignalR. El problema es la **higiene acumulada**
y el desfase de plataforma:

- Autorización inline duplicada (`if (!ctx.GetCurrentUserOrFail().IsAdmin)`) ~33 veces en 8 archivos.
- Handlers monolíticos: `SchoolsHandlers.cs` (887), `GenerateScheduleOrchestrator.cs` (446),
  `TeachersHandlers.cs` (431).
- Sin `Directory.Build.props`, sin `.editorconfig`, sin analyzers.
- Validación manual dispersa pese a tener FluentValidation registrado.
- Manejo de errores genérico con try-catch en Program.cs.
- Proyectos en `net8.0`; SDK .NET 10 instalado.

**Resultado esperado:** mismo comportamiento funcional, sobre .NET 10, con menos duplicación,
archivos más pequeños, gates de calidad activos y documentación veraz.

---

## Fases de ejecución

### Fase 0 — Validación de entorno
- Verificar build y tests en estado actual.
- Confirmar Docker operativo.
- Anotar warnings como línea base.

### Fase 1 — Cimientos de calidad y plataforma
- `Directory.Build.props` centralizado.
- `.editorconfig`.
- Upgrade a `net10.0` / C# 13.
- Actualizar paquetes NuGet.

### Fase 2 — Autorización: eliminar duplicaciones
- `AdminOnlyFilter : IEndpointFilter`.
- Reemplazar 33 líneas inline en 8 `*Feature.cs`.

### Fase 3 — Errores como ProblemDetails
- Excepciones de dominio explícitas.
- `IExceptionHandler` centralizado.
- Reemplazar catch-all genérico.

### Fase 4 — Romper monolitos de handlers
- `SchoolsHandlers.cs` → archivos individuales.
- `GenerateScheduleOrchestrator.cs` → colaboradores pequeños.
- `TeachersHandlers.cs` → separar CRUD / StageAssignments / SubjectHours.
- Tests en paralelo por handler movido sin cobertura.

### Fase 5 — Cerrar huecos de cobertura CRUD
- Tests para Assignments, Classrooms, Groups, Roles, Constraints.
- Casos multi-tenant.

### Fase 6 — Documentación y endurecimiento final
- Actualizar `CLAUDE.md`.
- Activar `TreatWarningsAsErrors` en todos los proyectos.
- Build sin warnings + suite completa de tests.

---

## Principios de ejecución

- Cada fase termina con `dotnet build -c Release` y `dotnet test tests/api/Lectivo.UnitTests` verdes.
- Cambios pequeños y enfocados; sin nuevas dependencias salvo justificación.
- No tocar archivos generados.
- Rama única: `jesusr/refactor` (rama actual).
- Un solo PR al final de todas las fases.
