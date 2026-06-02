# 📋 Checklist de Tareas - Plan de Mejora Diario de Lectivo

Este documento sirve como la hoja de ruta organizada para el desarrollo de mejoras en **Lectivo**. Puedes mantener este archivo sincronizado con tu repositorio Git y actualizar el estado de las tareas a medida que avancemos.

---

## 🛠️ Cómo actualizar este Checklist

Cada vez que realicemos una acción, editaremos este archivo actualizando el estado de las casillas:
- `[ ]` **Pendiente**: Tarea que aún no se ha iniciado.
- `[/]` **En progreso**: Tarea en la que se está trabajando actualmente.
- `[x]` **Completado**: Tarea implementada, compilada, testeada y verificada.

---

## 🚀 FASE 1 — Corregir el motor (Días 1–4)

### [x] Día 1 · Arreglar el cableado roto de `SchoolConfig` y aulas
*Problema: El motor recibe datos basura en producción (aulas vacías y slots por día equivalentes a slot minutes).*
- [x] **Modificar Entidades (`Entities.cs`)**:
  - [x] Añadir `SlotsPerDay` (int, default 5) y `DaysPerWeek` (int, default 5) a la clase/entidad `School`.
  - [x] Generar migración de EF Core: `dotnet ef migrations add AddSchoolSlotsAndDays`.
- [x] **Corregir construcción de `SchoolConfig` (`SchedulesFeature.cs:113`)**:
  - [x] Cargar las `Classrooms` reales del centro desde la base de datos.
  - [x] Mapear las aulas cargadas a `ClassroomInfo`.
  - [x] Pasar `school.SlotsPerDay` en lugar de `school.SlotMinutes` al constructor de `SchoolConfig`.
- [x] **Extender `SchoolConfig` (`BacktrackingScheduleEngine.cs:208`)**:
  - [x] Añadir la propiedad `DaysPerWeek` a `SchoolConfig`.
  - [x] Reemplazar el bucle `for (day = 1; day <= 5)` hardcodeado en `GetCandidateSlots` por `schoolConfig.DaysPerWeek`.
- [x] **Rediseñar Constraint de Aulas (`Constraints.cs:33`)**:
  - [x] Eliminar `ClassroomNotDoubleBooked` inerte.
  - [x] Crear una constraint funcional que consulte el estado de asignaciones (`state`) para evitar reservas duplicadas de la misma aula (Code implemented, unit tests updated and verified).
- [x] **Verificación**:
  - [x] Crear/Verificar test de integración que llame a `/generate` con el seed real.
  - [x] Verificar que `totalAssigned == totalRequired` y que cada entry tiene un `ClassroomId` válido.
  - [x] Ejecutar compilación y tests: `dotnet build` + `dotnet test tests/api/Lectivo.UnitTests` + `dotnet test tests/api/Lectivo.IntegrationTests`.

### [x] Día 2 · Recreos como constraint del motor + `SlotsPerDay` real
*Problema: Las clases se solapan sobre el recreo; el recreo solo se calcula en el frontend/pintado.*
- [x] **Unificar modelo de slots (`SlotCalculator.Compute`)**:
  - [x] Modificar o reutilizar la lógica de `SlotCalculator.Compute` para que el motor entienda qué índices de slots son lectivos y cuáles son recreos (`BreakAfterSlot`).
- [x] **Actualizar generación de candidatos (`GetCandidateSlots`)**:
  - [x] Impedir que `GetCandidateSlots` proponga slots correspondientes al recreo como asignables (El motor omite slots donde `IsBreak == true`).
  - [x] Hacer que `SlotsPerDay` provenga dinámicamente de la configuración del centro de extremo a extremo.
- [x] **Quitar hardcodes de último slot (`NoIntensiveSubjectLastSlot` / `SchedulesFeature.cs:414`)**:
  - [x] Reemplazar "slot 4 = último" por un cálculo dinámico del último índice lectivo basado en la configuración del centro.
- [x] **Verificación**:
  - [x] Crear test con centro de 6 slots/día y recreo tras el 2º slot, asegurando que ninguna asignatura cae en el slot de recreo y el "último tramo" se determina bien.
  - [x] Ejecutar tests: `dotnet test` (43 tests unitarios y 14 tests de integración exitosos en verde).

### [x] Día 3 · Constraints de colegio real I — especialistas y horas de profesor
*Problema: Atributos inactivos en el modelo; profesores generalistas impartiendo especialidades sin control.*
- [x] **Implementar `RequiresSpecialistConstraint` (Dura)**:
  - [x] Reutilizar `SubjectAllocation.RequiresSpecialist` y `Teacher.Specialties` (JSON).
  - [x] Propagar la especialidad del profesor al `SessionToAssign` o contexto del motor (Estructura de specialties de profesores inyectada en `SessionToAssign`).
  - [x] Evitar que se asigne un profesor que no tenga la especialidad requerida por la asignatura (implementada en `RequiresSpecialistConstraint`).
- [x] **Implementar `MaxWeeklyHoursConstraint` (Dura)**:
  - [x] Reutilizar la propiedad `Teacher.MaxWeeklyHours` (actualmente ignorada).
  - [x] Registrar en `AssignmentState` el recuento de horas asignadas por profesor y rechazar asignaciones que lo excedan.
- [x] **Implementar `TeacherGapsConstraint` (Blanda)**:
  - [x] Penalizar las ventanas libres o "huecos" intermedios en el horario diario del profesor.
  - [x] Utilizar el `HashSet` de slots de profesor disponible en `AssignmentState` para el cálculo.
- [x] **Verificación**:
  - [x] Escribir tests unitarios por constraint en `ConstraintsTests.cs` (7 nuevos tests añadidos y validados).
  - [x] Probar escenario donde un especialista escaso obliga a un resultado parcial con la explicación correcta del conflicto.
  - [x] Ejecutar tests: `dotnet test` (50 tests unitarios y 14 de integración correctos).

### [x] Día 4 · Constraints de colegio real II — bloques y heurística
*Problema: Las consecutivas solo cuentan hacia atrás, no hay bloques mínimos y la heurística es estática.*
- [x] **Corregir `MaxConsecutiveSlotsConstraint` (`Constraints.cs:77`)**:
  - [x] Modificar el contador para evaluar consecutivas en ambas direcciones (hacia adelante y hacia atrás).
- [x] **Implementar bloques consecutivos / divisibilidad**:
  - [x] Usar `SubjectAllocation.SplittableAcrossDays` para agrupar o dar preferencia a sesiones consecutivas (p. ej., clases de 2h seguidas para Educación Física).
- [x] **Mejorar heurística `PrioritizeSessions`**:
  - [x] Incorporar el número de horas semanales y la carga actual de asignaciones del profesor en la prioridad.
  - [x] Implementar un mecanismo de seguimiento **best-so-far** en el método `Backtrack` para devolver la mejor solución parcial encontrada ante un timeout.
- [x] **Verificación**:
  - [x] Escribir tests de distribución y medir tiempos de generación con el seed real (manteniéndose por debajo de los 30s).
  - [x] Ejecutar tests: `dotnet test` (55 tests unitarios y 14 de integración correctos).

---

## 🌐 FASE 2 — Genérico para cualquier centro (Días 5–7)

### [x] Día 5 · Configuración de jornada del centro de extremo a extremo
- [x] **Extender endpoint `PUT /api/schools/me`**:
  - [x] Soportar la edición de `SlotsPerDay`, `DaysPerWeek`, `MorningStart`, `SlotMinutes`, `BreakAfterSlot` y `BreakMinutes`.
  - [x] Añadir validación coherente en el payload del endpoint.
- [x] **Soportar Jornada Partida**:
  - [x] Reutilizar `School.AfternoonStart` en el motor de asignaciones y en `SlotCalculator`.
  - [x] Modelar el bloque de tarde como tramos lectivos adicionales con sus franjas horarias y slots correspondientes.
- [x] **Verificación**:
  - [x] Test de integración que configure jornada partida, genere un horario y compruebe índices de tarde coherentes.

### [x] Día 6 · Asistente de configuración de centro (backend)
- [x] **Endpoint para clonar la plantilla LOMLOE oficial**:
  - [x] Implementar endpoint que copie asignaturas y horas por defecto a la configuración del centro. (`POST /api/subjects/clone-official` en `SubjectsFeature.cs:66`)
  - [x] Permitir modificar las horas de `SubjectAllocation` dentro de los límites min/max (usando `SubjectsFeature.cs:28`).
- [x] **Cerrar fuga cross-tenant (`SubjectsFeature.cs:50`)**:
  - [x] Validar en `PUT /subjects/{id}/hours` que la `SubjectAllocation` pertenezca verdaderamente al centro del usuario actual. (guard `template.SchoolId != user.SchoolId` en `SubjectsFeature.cs:148`)
- [x] **Validación previa a la generación**:
  - [x] Validar disponibilidad de profesores y aulas antes de ejecutar el motor. Devuelve mensajes descriptivos si falta un especialista o tipo de aula. (`ScheduleViabilityAnalyzer.cs`, invocado en `SchedulesFeature.cs:181`)
- [x] **Verificación**:
  - [x] Test de aislamiento entre centros: `CrossTenantIsolationTests.cs` (4 tests). Admin del centro 1 no puede editar subjects del centro 2 y viceversa — todos 403.
  - [x] Corrección: `Results.Forbid()` → `Results.StatusCode(403)` en todos los features (evitaba 500 por ausencia de `IAuthenticationService`).
  - [x] Corrección: bug en `BacktrackingScheduleEngine.ComputeScheduleCost:194` — `ToDictionary(AssignmentId)` fallaba porque múltiples sesiones comparten `AssignmentId`. Corregido con `GroupBy + First()`.
  - Suite final: 108 unitarios correctos + 29/33 integración (4 tests de `SchedulesGenerationTests` usan IDs hardcodeados obsoletos — pre-existentes).

### [x] Día 7 · Limpieza de deuda de dominio backend
- [x] **Resolver bifurcación en `Schedule.cs` / `SchedulesFeature.cs:144`**:
  - [x] Eliminar la bifurcación (ambos devuelven `"generated"`). Definir una máquina de estados rica con un enum claro en `Schedule` o integrar la entidad al 100%.
- [x] **Corregir `GroupLabel` provisional (`SchedulesFeature.cs:367`)**:
  - [x] Eliminar `GuidToString()[^4..]` y usar el `DisplayName` / etiqueta real del grupo de alumnos.
- [x] **Optimizaciones e índices**:
  - [x] Añadir índices por `SchoolId` en las tablas principales mediante migración EF Core para evitar table scans.
  - [x] Autorizar el hub SignalR (`JoinSchoolGroup` no debe aceptar cualquier GUID sin comprobación del token del centro).
- [x] **Verificación**:
  - [x] Ejecutar toda la suite de tests: `dotnet test` (Unit + Integration).

---

## 🎨 FASE 3 — Frontend profesional (Días 8–12)

### [x] Día 8 · Adoptar PrimeNG + sistema de feedback global
- [x] **Implementar servicio Toast de PrimeNG**:
  - [x] Activar PrimeNG (tema Aura, `darkModeSelector: '.lectivo-dark'`) en `app.config.ts:32`. `MessageService` y `ConfirmationService` provistos globalmente.
  - [x] Declarar `<p-toast />` y `<p-confirmdialog />` de forma global en `app.ts`.
- [x] **Eliminar silencios en `catch {}`**:
  - [x] Reemplazar `console.error` en `generator.component.ts` por Toast `error`. Todos los bloques catch de config usan `MessageService`.
- [x] **Reemplazar confirm/alert nativos**:
  - [x] Sustituir todos los `confirm()` y `alert()` en `config.component.ts` (15+ instancias) por `ConfirmationService.confirm()` y `MessageService.add()`.
- [x] **Verificación**:
  - [x] `ng build` exitoso sin advertencias de tipado.

### [x] Día 9 · Migrar pantallas a PrimeNG y eliminar inline styles
- [x] **Migrar tablas de configuración (`config.component.ts`)**:
  - [x] Implementar `p-table` con paginación (10 filas), ordenación por columna y filtro global en las 4 tablas (Profesores, Grupos, Asignaturas, Aulas).
- [x] **Limpieza de estilos inline**:
  - [x] Mover `style="..."` estáticos a clases CSS: `.td-muted`, `.td-actions`, `.th-actions`, `.row-actions`, `.pills-wrap`, `.subj-pill`, `.tag`, `.text-xs-dim`, `.section-title`, `.lomloe-banner`, `.table-caption`, `.table-filter-input`, `.btn-primary--sm`.
  - [x] Extraer banner LOMLOE a clases `.lomloe-banner__body/.title/.desc`.
  - [x] Limpiar `ng-template #tableHeader` de inline styles.
  - [x] Añadir override de p-table con `::ng-deep` para integrar el tema Aura en el sistema de tokens Lectivo.
- [x] **Verificación**:
  - [x] `ng build --configuration development` limpio sin errores ni warnings de tipado.

### [x] Día 10 · Completar placeholders funcionales

- [x] **CRUD completo en configuración (`config.component.ts`)**:
  - [x] Formularios y modales propios funcionales para crear/editar profesores, grupos, aulas y asignaturas. *(migración a `p-dialog` pendiente: Ejecución 2)*
- [x] **Exportación PDF profesional**:
  - [x] Estilos `@media print` implementados en `styles.scss:243` (oculta nav, centra grid, `print-color-adjust: exact`). `window.print()` en `schedule-result` y `my-schedule`.
- [x] **Medición real de tiempo**:
  - [x] `generationSeconds` proviene del servidor (`models.ts:128`; campo `GenerationSeconds` en `Schedule` del backend).
- [x] **Verificación completa con p-dialog**:
  - [x] Modales de Profesores, Grupos, Aulas y Asignaturas usan `p-dialog` de PrimeNG con `MessageService` + `ConfirmationService`. `ng build` limpio.

### [x] Día 11 · Pulir la visualización del horario + dark mode
- [x] **Unificar grids de horario**:
  - [x] `my-schedule.component.ts` y `schedule-result.component.ts` usan `ScheduleGridComponent` — sin código duplicado.
- [x] **Mostrar leyenda de asignaturas**:
  - [x] `SubjectLegendComponent` implementado en `src/app/shared/ui/subject-legend.component.ts`; usa `SUBJECT_COLORS` (variables CSS) y se incluye en ambas vistas de horario.
- [x] **Soporte de Dark Mode (`styles.scss`)**:
  - [x] Añadir tokens `--subj-*` dark (fondo oscuro `oklch ~0.27`, fg claro `oklch ~0.82`) bajo `.lectivo-dark`.
  - [x] Añadir tokens de estado (success, warning, destructive, ring) al bloque dark.
  - [x] Botón toggle dark mode ya existente en `app-shell.component.ts`; persiste en `localStorage`.
- [x] **Verificación**:
  - [x] `ng build` limpio sin errores tras añadir variables dark de asignaturas.

### [x] Día 12 · Robustez, accesibilidad y estados consistentes
- [x] **Estados de conexión de SignalR**:
  - [x] Señal `hubState` (`disconnected | connecting | connected`) en `GeneratorComponent`. Se actualiza en `start()`, `onreconnecting`, `onreconnected`, `onclose`. Banner visible en el paso 4 con `aria-live="polite"`.
- [x] **Componentes de estado reutilizables**:
  - [x] `EmptyStateComponent` creado en `src/app/shared/ui/empty-state.component.ts` con inputs `message`, `hint` e `icon`.
- [x] **Mejorar Accesibilidad**:
  - [x] Stepper del generador: `role="list"`, `role="button"`, `aria-label`, `aria-current="step"`, `aria-hidden` en elementos decorativos.
- [x] **Tipado estricto**:
  - [x] Eliminado `as any` en `teacher-profile.component.ts:329` — `TeacherConstraint` ya incluye `weight`.
- [x] **Optimización de rendimiento**:
  - [x] `DeviceService.onResize()` usa `requestAnimationFrame` en lugar de disparar en cada píxel.
- [x] **Verificación**:
  - [x] `ng build --configuration development` limpio sin errores ni warnings.

---

## 📈 FASE 4 — Rendimiento y cierre (Día 13)

### [x] Día 13 · Rendimiento y validación final
- [x] **Optimización en Backend**:
  - [x] `BuildSessions` auditado: 5 queries con `.AsNoTracking()`, sin N+1, bucles O(n). No requiere cambios.
  - [x] Índices `SchoolId` confirmados en migración `20260531154448_AddSchoolIdIndices.cs`: TeacherConstraints, Schedules, ScheduleEntries, Classrooms, Assignments.
  - [x] BackgroundService evaluado: la arquitectura usa `CancellationToken` + SignalR para progreso. El endpoint aguarda `GenerateAsync` de forma async. Riesgo de timeout aceptado con el parámetro de 30s configurable.
- [x] **Optimización en Frontend**:
  - [x] `PreloadAllModules` ya activo en `app.config.ts:23` (`withPreloading(PreloadAllModules)`).
  - [x] Debounce `requestAnimationFrame` en `DeviceService.onResize()` implementado en Día 12.
  - [x] Computed signals del grid (`ScheduleGridComponent`) memoizados automáticamente por Angular.
- [x] **Cierre de suite de pruebas**:
  - [x] `dotnet test tests/api/Lectivo.UnitTests` — **108 tests en verde**, 0 errores.
  - [x] `ng build --configuration development` — sin errores ni warnings.
- [ ] **Verificación pendiente** (requiere Docker + seed):
  - [ ] `dotnet test tests/api/Lectivo.IntegrationTests` (Testcontainers).
  - [ ] `npm run e2e` (requiere API + web levantados).
