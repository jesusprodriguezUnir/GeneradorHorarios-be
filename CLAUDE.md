# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proyecto

**Lectivo** — generador automático de horarios escolares para colegios de primaria bajo LOMLOE (Madrid). Monorepo con backend .NET 8 y frontend Angular 21.

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

### Frontend (apps/web)

```bash
cd apps/web
npm install          # Solo la primera vez
ng serve             # Dev server en http://localhost:4200
ng build             # Build de producción
ng build --watch --configuration development
```

### Tests

```bash
# Backend — unitarias (sin BD, ~1s)
dotnet test tests/api/Lectivo.UnitTests

# Backend — integración (requiere Docker para Testcontainers)
dotnet test tests/api/Lectivo.IntegrationTests

# Frontend — E2E con Playwright (requiere API + web levantados o usa webServer)
cd apps/web
npm run e2e
npm run e2e:ui   # modo UI
```

La solución `Lectivo.slnx` agrupa API + proyectos de test.

## Arquitectura

### Comunicación Frontend → Backend

El frontend usa `HttpClient` con un `AuthInterceptor` que inyecta el header `X-User-Email` en todas las peticiones (auth simulada, sin JWT). El backend identifica al usuario por ese header.

Para el flujo de generación de horario, la comunicación es híbrida:
1. `POST /api/schedules/generate` arranca la generación en background
2. El cliente abre una conexión SignalR a `/hubs/generation` y recibe eventos `GenerationProgress` en tiempo real
3. Al completarse se emite el resultado final; el cliente redirige a `/horarios/{id}`

### Motor de generación (BacktrackingScheduleEngine)

Resuelve el problema NP-duro de asignación de horarios mediante backtracking con:
- **Heurística fail-first**: `PrioritizeSessions()` ordena las sesiones más difíciles primero
- **Constraints duras** (`IHardConstraint`): un profesor no puede estar en dos sitios, un aula no puede estar doble-reservada, etc.
- **Constraints blandas** (`ISoftConstraint`): preferencias con penalización numérica
- **Timeout configurable** + `CancellationToken` para no bloquear indefinidamente
- Devuelve `ScheduleResult` con estado `Complete | Partial | Failed` y explicaciones de conflictos

### Vertical Slice (backend)

Cada feature en `apps/api/Features/` es un slice autónomo con sus propios endpoints, DTOs y queries EF Core. No hay repositorios genéricos ni capas de aplicación separadas — la lógica de negocio simple va directamente en el endpoint handler; solo la generación de horarios tiene su propio servicio de dominio (`IScheduleEngine`).

### Angular 20 Zoneless + Signals

- `provideZonelessChangeDetection()` activo → **no usar NgZone ni `markForCheck()`**
- Estado reactivo con `signal()` y `computed()` en lugar de BehaviorSubject cuando sea posible
- Todos los componentes son `standalone: true`
- Lazy loading por ruta en `app.routes.ts`

### Usuarios demo

| Email | Rol |
|-------|-----|
| `elena.castro@ceip-miguel-hernandez.es` | Jefatura / admin |
| `laura.fernandez@ceip-miguel-hernandez.es` | Profesora / teacher |

## Decisiones de diseño relevantes

- La especificación LOMLOE y los ADRs están en `doc/horarios-escolares/docs/`; consultarlos antes de cambiar reglas de negocio del motor.
- El seed de datos (CEIP Miguel Hernández, Madrid) se aplica en `DbInitializer` al arrancar la API.
- La base de datos de desarrollo corre en Docker con credenciales fijas en `appsettings.json`; no hay gestión de secretos para el entorno local.
- TypeScript configurado en modo ultra-strict (`strict`, `noImplicitReturns`, `strictTemplates`); el compilador Angular (`ng build`) es la fuente de verdad para errores de tipado.
