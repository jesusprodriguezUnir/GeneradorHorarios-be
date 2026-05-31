# Índice de Feature Specs — HorariosEscolares

> Todas las specs siguen la [Constitución v1.1](../constitution/CONSTITUTION.md)  
> Stack: Angular 20 · Zoneless · .NET 8 · Supabase · Vercel · Railway

---

## Estado de specs

| ID | Nombre | Fase | Prioridad | Estado | Estimación |
|----|--------|------|-----------|--------|------------|
| [SPEC-001](SPEC-001-scaffolding-angular.md) | Scaffolding Angular 20 PWA + Vercel | 0 | P0 | Draft | 1d |
| [SPEC-002](SPEC-002-scaffolding-api.md) | Scaffolding .NET API + Railway + Docker | 0 | P0 | Draft | 1d |
| [SPEC-003](SPEC-003-supabase-schema.md) | Supabase: schema + RLS + Auth | 0 | P0 | Draft | 2d |
| [SPEC-004](SPEC-004-cloudflare-dns.md) | Cloudflare: dominio + DNS routing | 0 | P0 | Draft | 0.5d |
| [SPEC-010](SPEC-010-school-registration.md) | Registro de colegio | 1 | P0 | Draft | 1d |
| [SPEC-011](SPEC-011-auth-login.md) | Login magic link + Google OAuth | 1 | P0 | Draft | 1d |
| [SPEC-012](SPEC-012-roles.md) | Gestión de roles admin/teacher | 1 | P0 | Draft | 1d |
| [SPEC-013](SPEC-013-invite-teachers.md) | Invitar profesores por email | 1 | P1 | Draft | 1d |
| [SPEC-020](SPEC-020-onboarding-wizard.md) | Onboarding wizard (15 minutos) | 2 | P0 | Draft | 3d |
| [SPEC-021](SPEC-021-teachers-crud.md) | CRUD profesores + especialidades | 2 | P1 | Draft | 2d |
| [SPEC-022](SPEC-022-groups-crud.md) | CRUD grupos/cursos | 2 | P1 | Draft | 1d |
| [SPEC-023](SPEC-023-classrooms-crud.md) | CRUD aulas | 2 | P1 | Draft | 1d |
| [SPEC-024](SPEC-024-lomloe-template.md) | Plantilla LOMLOE Madrid precargada | 2 | P0 | Draft | 2d |
| [SPEC-025](SPEC-025-assignments.md) | Asignación profesor→asignatura→grupo | 2 | P0 | Draft | 2d |
| [SPEC-026](SPEC-026-schedule-config.md) | Configuración jornada y franjas | 2 | P0 | Draft | 1d |
| [SPEC-030](SPEC-030-engine-backtracking.md) | Motor backtracking v1 | 3 | P0 | Draft | 5d |
| [SPEC-031](SPEC-031-hard-constraints.md) | Hard constraints: no solapamiento | 3 | P0 | Draft | 2d |
| [SPEC-032](SPEC-032-normative-constraints.md) | Hard constraints: horas normativa | 3 | P0 | Draft | 2d |
| [SPEC-033](SPEC-033-soft-constraints.md) | Soft constraints: distribución equitativa | 3 | P1 | Draft | 2d |
| [SPEC-034](SPEC-034-conflict-explanation.md) | Explicación de conflictos en lenguaje natural | 3 | P0 | Draft | 2d |
| [SPEC-035](SPEC-035-signalr-progress.md) | Progreso en tiempo real (SignalR) | 3 | P1 | Draft | 1d |
| [SPEC-036](SPEC-036-schedule-versioning.md) | Versionado de horarios | 3 | P0 | Draft | 1d |
| [SPEC-040](SPEC-040-my-schedule.md) | "Mi horario" — vista profesor (mobile first) | 4 | P0 | Draft | 3d |
| [SPEC-041](SPEC-041-schedule-view-group.md) | Vista horario completo — por grupo | 4 | P0 | Draft | 2d |
| [SPEC-042](SPEC-042-schedule-view-teacher.md) | Vista horario completo — por profesor | 4 | P1 | Draft | 1d |
| [SPEC-043](SPEC-043-schedule-view-classroom.md) | Vista horario completo — por aula | 4 | P2 | Draft | 1d |
| [SPEC-044](SPEC-044-manual-edit.md) | Edición manual de celdas con validación | 4 | P1 | Draft | 3d |
| [SPEC-045](SPEC-045-conflict-visual.md) | Detección visual de conflictos | 4 | P1 | Draft | 1d |
| [SPEC-050](SPEC-050-export-pdf-teacher.md) | Export PDF horario individual | 5 | P1 | Draft | 2d |
| [SPEC-051](SPEC-051-export-pdf-school.md) | Export PDF horario completo colegio | 5 | P1 | Draft | 1d |
| [SPEC-052](SPEC-052-public-link.md) | Link público del horario (sin login) | 5 | P2 | Draft | 1d |
| [SPEC-060](SPEC-060-substitutions.md) | Dashboard de guardias diario | 6 | P1 | Draft | 3d |
| [SPEC-061](SPEC-061-substitution-log.md) | Registro de sustituciones | 6 | P1 | Draft | 2d |
| [SPEC-062](SPEC-062-load-analysis.md) | Análisis de carga del profesorado | 6 | P2 | Draft | 2d |
| [SPEC-063](SPEC-063-normative-report.md) | Informe cumplimiento normativo PDF | 6 | P2 | Draft | 2d |

---

## Leyenda

| Prioridad | Descripción |
|-----------|-------------|
| P0 | Bloqueante — el piloto no funciona sin esto |
| P1 | MVP — necesario antes de enseñar a colegios |
| P2 | Post-MVP — añadir cuando haya tracción |

| Estado | Descripción |
|--------|-------------|
| Draft | En redacción, no lista para implementar |
| Ready | Revisada, criterios claros, lista para implementar |
| In Progress | En desarrollo activo |
| Done | Implementada y criterios verificados |
