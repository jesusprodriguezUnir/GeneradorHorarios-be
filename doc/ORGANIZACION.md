# HorariosEscolares — Mapa del proyecto

> Fecha de organización: 30 de mayo de 2026

---

## Estructura

```
Generadorhorarios/
├─ horarios-escolares/     ← Monorepo de código fuente + documentación técnica
│   ├─ src/
│   │   ├─ api/            ← Backend: API .NET 8 (C#)
│   │   └─ web/            ← Frontend: Angular PWA
│   ├─ docs/
│   │   ├─ specs/          ← Especificaciones técnicas (SPEC-001 a SPEC-040)
│   │   ├─ adr/            ← Decisiones de arquitectura (ADR-001-003)
│   │   ├─ constitution/   ← CONSTITUTION.md — reglas del proyecto
│   │   └─ database/       ← Schema SQL y seed LOMLOE Madrid para Supabase
│   ├─ tests/
│   │   └─ Domain.Tests/   ← Tests de constraints y motor
│   ├─ README.md
│   ├─ GITHUB-SETUP.md     ← Instrucciones para crear el repo en GitHub
│   └─ .gitignore
└─ marketing/              ← Materiales de marca, estrategia y presentaciones
    ├─ presentaciones/
    │   ├─ HorariosEscolares-01-Producto.pptx
    │   ├─ HorariosEscolares-02-Arquitectura-Tecnica.pptx
    │   └─ HorariosEscolares-03-Estrategia-Negocio.pptx
    ├─ branding/
    │   ├─ horarios-escolares-logo.svg
    │   └─ horarios-escolares-logo-horizontal.svg
    └─ horarios-escolares-estrategia.md
```

---

## Resumen del producto

**HorariosEscolares.es** — SaaS de generación automática de horarios para colegios de primaria (normativa LOMLOE, mercado Madrid/España).

- **Tagline:** *"El horario de tu colegio, en 15 minutos."*
- **Modelo:** B2B, directores y jefatura de estudios de colegios
- **Precio piloto:** 9 €/colegio/mes (escalable a 29 €)
- **KPI del piloto:** 5 colegios usando la herramienta antes del verano

---

## Stack técnico

| Capa | Tecnología |
|------|------------|
| Backend | .NET 8, C#, arquitectura vertical slice (Minimal API + Carter) |
| Motor de horarios | Backtracking con constraint propagation (sin OR-Tools por ahora) |
| Frontend | Angular 17+ PWA |
| Base de datos | PostgreSQL vía Supabase (auth incluida) |
| Despliegue | Vercel (frontend) + Railway (backend) + Cloudflare (DNS/CDN) |
| CI/CD | GitHub Actions |

---

## Estado del código (a mayo 2026)

### Lo que está implementado
- `Domain/Entities/Schedule.cs` — entidades del dominio
- `Domain/Constraints/Constraints.cs` — lógica de restricciones LOMLOE
- `Domain/Services/IScheduleEngine.cs` — interfaz del motor
- `Infrastructure/Engine/BacktrackingScheduleEngine.cs` — motor de backtracking completo
- `Features/Schedules/ScheduleFeature.cs` — endpoint de generación (Minimal API)
- `Program.cs` — arranque de la API con Supabase, CORS, SignalR, Swagger
- `tests/Domain.Tests/Constraints/ConstraintsTests.cs` — tests de constraints
- `docs/database/seed-lomloe-madrid.sql` — seed completo de asignaturas y horas LOMLOE para Madrid

### Lo que tiene estructura pero está vacío (por construir)
- `src/web/` — Angular (solo `environment.example.ts`)
- `src/api/Domain/ValueObjects/`, `Application/`, `Infrastructure/Persistence/`, `API/Endpoints/`, `API/Hubs/`, `API/Middleware/`
- `tests/Application.Tests/`, `tests/Integration.Tests/`

### Specs pendientes de implementar (en `docs/specs/`)
| SPEC | Descripción |
|------|-------------|
| SPEC-001 | Scaffolding Angular (estructura inicial de la app) |
| SPEC-002 | Scaffolding API (estructura completa de la API) |
| SPEC-003 | Schema Supabase (migraciones, RLS, tablas) |
| SPEC-020 | Wizard de onboarding (alta de colegio, profesores, grupos) |
| SPEC-030 | Motor de backtracking ✅ implementado |
| SPEC-034 | Explicación de conflictos en lenguaje natural |
| SPEC-040 | Vista "Mi horario" para el profesor |

---

## Marketing y marca

Ver `marketing/horarios-escolares-estrategia.md` para el detalle completo.

**Paleta de color:**
- Navy `#1a3a5c` — fondo principal
- Teal `#4ecdc4` — acento primario, CTAs
- Gold `#f9c846` — éxito, generado OK

**Canales:** LinkedIn (principal, 2 posts/semana) + Twitter/X + YouTube (SEO)

**Infraestructura — coste piloto:** ~8 €/año (solo dominio `.es`)

---

## Próximos pasos sugeridos

1. **Inicializar el repositorio git** — seguir `horarios-escolares/GITHUB-SETUP.md`
2. **Implementar SPEC-002** — completar el scaffolding de la API (.NET)
3. **Implementar SPEC-001** — scaffolding del frontend Angular
4. **Aplicar SPEC-003** — crear las migraciones de Supabase
5. **Implementar SPEC-020** — wizard de onboarding (primer flujo de usuario)
6. **Implementar SPEC-034** — explicación de conflictos
7. **Implementar SPEC-040** — vista del profesor

---

## Nota técnica

Al organizar este proyecto se detectó y corrigió un bug: los ZIP originales incluían una carpeta con nombre literal `{docs/{specs,constitution,adr},src/{api/...}}/` — resultado de ejecutar `mkdir -p {docs/{...}}` con expansión de llaves de bash en PowerShell (que no la soporta). Esa carpeta estaba vacía; las carpetas reales (`docs/`, `src/`, `tests/`) contienen el código correcto y no se vieron afectadas.
