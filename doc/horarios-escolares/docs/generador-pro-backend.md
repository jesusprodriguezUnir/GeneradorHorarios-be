# Generador PRO — cambios de backend pendientes

El rediseño del **Generador** (diseño Lectivo "pro", `apps/web/src/app/features/generator/`)
introduce una experiencia muy por delante de lo que el backend .NET soporta hoy.
El front-end ya está implementado de forma fiel; donde el backend no llega, el cliente
**simula** el comportamiento de forma determinista (igual que el prototipo de diseño).

Este documento enumera el trabajo de backend necesario para que esas funciones dejen de
ser simuladas y pasen a ser reales. Está ordenado por dependencia / valor.

## Estado actual del front-end

| Paso | Front-end (hoy) | Respaldo backend |
|------|-----------------|------------------|
| 1 · Configuración | Real | `PUT /schools/me` (existe) |
| 2 · Matriz de asignaciones | Editable en cliente; estado **solo en memoria** | ❌ no hay write API |
| 3 · Restricciones ricas (9 tipos, dura/blanda, prioridad) | Real en cliente; estado **solo en memoria** | ⚠️ solo `unavailable`/`preference` |
| 4 · Objetivos + motor en vivo + 3 candidatas | Animación y puntuación **simuladas**; la generación real usa el endpoint actual | ⚠️ una sola solución, sin objetivos |
| Resultado · Cuadro de calidad | Métricas de la candidata elegida (`GenerationStateService`) | ❌ no se persisten métricas |

## 1. Escritura de asignaciones (profesor × grupo × asignatura)

Hoy `GET /assignments` es de solo lectura (`AssignmentSummary[]`). La matriz del paso 2
necesita persistir cambios.

- `POST /assignments` `{ teacherId, groupId, allocationId }` → crea/actualiza la asignación.
- `DELETE /assignments/{id}` o `PUT /assignments` con `teacherId: null` para desasignar.
- Validación: respetar `maxWeeklyHours` del docente (devolver 409 con detalle de sobrecarga,
  que el front ya sabe representar).
- Sugerencia: endpoint `POST /assignments/autocomplete` que aplique la heurística
  "menos carga primero entre especialistas" en servidor (hoy se hace en cliente,
  `StepAssignmentsComponent.autocomplete`).

## 2. Restricciones ricas

`TeacherConstraint` solo modela `unavailable | preference` con `dayOfWeek/slotIndex/weight`.
El diseño define **9 tipos** agrupados en 4 categorías (ver
`apps/web/src/app/core/generation.model.ts → GEN_CTYPES`):

`no-disp`, `turno`, `media`, `dificil`, `seguidas`, `no-tramo`, `aula`, `recreo`, `codoc`.

Propuesta de modelo (`Constraint`):

```
Id, SchoolId, Type (enum), Kind ('hard' | 'soft'), Priority (1-3, solo soft)
TeacherId?, SubjectKey?, GroupId?, RoomId?
DayOfWeek?, SlotIndex?, Turno?, Pos?, MaxConsecutive?, Scope?
```

- `POST /constraints`, `DELETE /constraints/{id}` aceptando este payload extendido.
- El motor debe tratar `hard` como restricción **dura** (poda) y `soft` como penalización
  ponderada por `priority`.

## 3. Objetivos de optimización

`POST /schedules/generate` debe aceptar pesos de objetivo:

```
{ academicYear, timeoutSeconds, objectives: { huecos, mananas, agrupacion, equilibrio, aulas } }
```
(valores 0-3: Ignorar/Bajo/Medio/Alto — ver `GEN_OBJECTIVES`/`GEN_WEIGHTS`).

El `BacktrackingScheduleEngine` debería traducir cada objetivo a `ISoftConstraint`
con factor de peso, de modo que reordenen el resultado.

## 4. Generación multi-candidata

Hoy `generate` devuelve **una** solución. El diseño compara **3 candidatas** puntuadas
y deja elegir una.

- Que el motor genere N soluciones (p. ej. variando semillas/estrategias) y las puntúe.
- Respuesta: `{ candidates: [{ id, name, tag, score, metrics, scheduleId }] }`.
- `POST /schedules/{id}/choose` para fijar la elegida como horario activo.
- Mientras tanto, el front simula 3 candidatas (`GEN_CANDIDATES`) puntuadas con los
  objetivos del usuario; al elegir, navega al `scheduleId` real ya generado.

## 5. Métricas de calidad persistidas

El Cuadro de calidad (`QualityScorecardComponent`) muestra 6 métricas + % de preferencias.
Hoy provienen de la candidata simulada guardada en `GenerationStateService`.

- Persistir en el horario generado: `qualityScore` y `metrics` por objetivo
  (`huecos, mananas, agrupacion, equilibrio, aulas, prefs`).
- Exponer en `GET /schedules/{id}` para que el scorecard use datos reales.

## 6. Progreso en vivo por SignalR

El motor en vivo (`StepGenerationComponent`, fase `running`) hoy se anima con un timer
en cliente. Para reflejar el cálculo real, el hub `/hubs/generation` debería emitir un
evento enriquecido:

```
GenerationProgress { phase, percentage, score, iterations, candidatesExplored, rulesSatisfied, conflicts }
```

El front ya tiene las "ranuras" (contadores, barra, mini-grid) listas para enchufarlas.

---

**Resumen de prioridad:** 1 (asignaciones) y 2 (restricciones) desbloquean datos reales;
3-4-5 convierten el motor en realmente potente; 6 es pulido de experiencia.
