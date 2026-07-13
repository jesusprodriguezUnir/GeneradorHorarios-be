# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proyecto

**Lectivo** — generador automático de horarios escolares para colegios de primaria bajo LOMLOE (Madrid). Backend .NET 10 · el frontend SPA vive en [GeneradorHorarios-spa](https://github.com/jesusprodriguezUnir/GeneradorHorarios-spa).

## Comandos principales

### Requisito previo: base de datos

```bash
docker compose up -d
# Esperar ~15 s hasta que el healthcheck pase
docker compose ps   # → "healthy"
```

### Backend (apps/api)

```bash
cd apps/api
dotnet run           # API en http://localhost:5000 · Swagger en /swagger
dotnet build         # Compilar sin ejecutar
dotnet ef migrations add <Nombre>   # Nueva migración EF Core
dotnet ef database update           # Aplicar migraciones manualmente
```

La API aplica migraciones y siembra datos demo automáticamente al arrancar.

### Tests

```bash
# Unitarias (sin BD, ~1s)
dotnet test tests/api/Lectivo.UnitTests

# Integración (requiere Docker para Testcontainers)
dotnet test tests/api/Lectivo.IntegrationTests
```

La solución `Lectivo.slnx` agrupa API + proyectos de test.

## Arquitectura

### Comunicación Frontend → Backend

El frontend usa `HttpClient` con un `AuthInterceptor` que inyecta el header `X-User-Email` en todas las peticiones (auth simulada, sin JWT). El backend identifica al usuario por ese header.

Para el flujo de generación de horario, la comunicación es híbrida:
1. `POST /api/schedules/generate` arranca la generación en background vía Hangfire
2. El cliente abre una conexión SignalR a `/hubs/generation` y recibe eventos `GenerationProgress` en tiempo real
3. Al completarse se emite el resultado final; el cliente redirige a `/horarios/{id}`

### Capas del backend

El código se organiza en tres capas bajo `src/`:

| Capa | Proyecto | Responsabilidad |
|------|----------|-----------------|
| **Domain** | `HorariosEscolares.Domain` | Entidades, abstractions, servicios de dominio, reglas LOMLOE |
| **Application** | `HorariosEscolares.Application` | Handlers MediatR (CQRS), validadores FluentValidation, behaviors, excepciones |
| **Infrastructure** | `HorariosEscolares.Infrastructure` | Persistencia EF Core, Hangfire, motor de generación, normativa |

`apps/api/` es la capa de presentación — endpoints Minimal API que delegan a MediatR.

### MediatR / CQRS

Las operaciones se modelan como commands/queries en `src/HorariosEscolares.Application/Features/`. Cada handler implementa `IRequestHandler<TCommand/Query, TResponse>`. Registrado en `Application/DependencyInjection.cs` con dos pipeline behaviors:
- `LoggingBehavior` — log de nombre y duración del request
- `ValidationBehavior` — ejecuta automáticamente los FluentValidation validators antes del handler

### FluentValidation

Validadores en `Application/Features/*/Validators/`. Auto-registrados vía `AddValidatorsFromAssembly`. Errores de validación se lanzan como `ValidationException` y se capturan en el middleware de errores.

### Autenticación y autorización

- **`DevAuthMiddleware`** (`Features/Auth/DevAuthMiddleware.cs`): resuelve el usuario desde headers (`X-Api-Key`, `X-User-Id`, `X-User-Email`). No hay JWT ni ASP.NET Identity.
- **`AdminOnlyFilter`** (`Features/Auth/AdminOnlyFilter.cs`): `IEndpointFilter` que verifica `ICurrentUser.IsAdmin`. Exposición como `.RequireAdmin()` en 37 endpoints de escritura.

### Manejo de errores

Middleware centralizado en `Program.cs` que captura excepciones específicas y devuelve respuestas JSON custom:
- `ForbiddenAccessException` → 403
- `NotFoundException` → 404
- `ValidationException` → 400 (con detalles de validación)
- `Exception` genérica → 500

### Hangfire

Jobs en background para generación de horarios. Configurado con SQL Server storage, 2 workers, colas `"default"` y `"schedules"`. Dashboard en `/hangfire` (solo no-production).

### Motor de generación (BacktrackingScheduleEngine)

Resuelve el problema NP-duro de asignación de horarios mediante backtracking con:
- **Heurística fail-first**: `PrioritizeSessions()` ordena las sesiones más difíciles primero
- **Constraints duras** (`IHardConstraint`): un profesor no puede estar en dos sitios, un aula no puede estar doble-reservada, etc.
- **Constraints blandas** (`ISoftConstraint`): preferencias con penalización numérica
- **Timeout configurable** + `CancellationToken` para no bloquear indefinidamente
- Devuelve `ScheduleResult` con estado `Complete | Partial | Failed` y explicaciones de conflictos

### Usuarios demo

| Email | Rol |
|-------|-----|
| `elena.castro@ceip-miguel-hernandez.es` | Director (Admin) |
| `maria.garcia@ceip-miguel-hernandez.es` | Jefe de Estudios (Admin) |
| `antonio.lopez@ceip-miguel-hernandez.es` | Secretario (Admin) |
| `laura.fernandez@ceip-miguel-hernandez.es` | Profesor (Teacher) |
| `ana.garcia@ceip-miguel-hernandez.es` | Tutor (Teacher) |
| `beatriz.lopez@ceip-miguel-hernandez.es` | Coordinador de ciclo (Teacher) |
| `lucia.martin@ceip-miguel-hernandez.es` | Orientador (Other) |

### Roles disponibles (catálogo global)

| Code | Name | Kind | Descripción |
|------|------|------|-------------|
| `director` | Director | Admin | Máxima responsabilidad del centro |
| `jefe_estudios` | Jefe de Estudios | Admin | Coordinación académica y horarios |
| `secretario` | Secretario | Admin | Gestión administrativa y documental |
| `profesor` | Profesor | Teacher | Docencia general |
| `tutor` | Tutor | Teacher | Profesor con tutoría de un grupo |
| `coordinador_ciclo` | Coordinador de ciclo | Teacher | Coordinación pedagógica de un ciclo |
| `orientador` | Orientador | Other | Orientación educativa y psicopedagógica |

Los usuarios demo actuales (`elena.castro@...` → `RoleId = Director`, `laura.fernandez@...` → `RoleId = Profesor`) se migran automáticamente desde el antiguo string `Role` via la migración `AddRolesTable`. `ICurrentUser` expone `IsAdmin`/`IsTeacher` derivados de `RoleKind`.

## Decisiones de diseño relevantes

- La especificación LOMLOE y los ADRs están en `doc/horarios-escolares/docs/`; consultarlos antes de cambiar reglas de negocio del motor.
- El seed de datos (CEIP Miguel Hernández, Madrid) se aplica en `DbInitializer` al arrancar la API.
- La base de datos de desarrollo corre en Docker con credenciales fijas en `appsettings.json`; no hay gestión de secretos para el entorno local.
- El frontend SPA que consume esta API vive en [GeneradorHorarios-spa](https://github.com/jesusprodriguezUnir/GeneradorHorarios-spa) (Angular 21 Zoneless + Signals).

# Copilot instructions
- Use C# 13 and .NET 10 conventions.
- Keep changes minimal and focused.
- Do not introduce new dependencies without explaining why.
- Run `dotnet build -c Release` after code changes.
- Run relevant tests only unless asked for the full suite.
- Prefer Aspire service defaults when adding services.
- Do not modify generated files.
