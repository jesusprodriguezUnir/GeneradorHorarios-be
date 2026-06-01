# ADR-004: Evolución del motor de horarios — benchmark competitivo y arquitectura two-phase

**Fecha:** Junio 2026  
**Estado:** Propuesta  
**Decidido por:** Equipo (proyecto unipersonal)  
**Relacionado con:** [SPEC-030](../doc/horarios-escolares/docs/specs/SPEC-030-engine-backtracking.md), [SPEC-031](../doc/horarios-escolares/docs/specs/SPEC-031-hard-constraints.md), [SPEC-033](../doc/horarios-escolares/docs/specs/SPEC-033-soft-constraints.md)

---

## Contexto

El motor actual (`BacktrackingScheduleEngine`) resuelve el problema de asignación de horarios mediante **backtracking con propagación de constraints y heurística fail-first**. Es correcto —garantiza que no se viole ninguna hard constraint— pero tiene dos limitaciones algorítmicas fundamentales:

1. **Para en la primera solución completa**. En cuanto `result.Count == sessions.Count`, el algoritmo retorna sin intentar mejorar las penalizaciones soft globales. El orden de asignación (greedy local, menor penalización por candidato individual) determina la calidad del horario, lo que no es óptimo.
2. **No tiene análisis de viabilidad previo**. Los conflictos solo se detectan y explican *a posteriori* (`BuildConflictExplanations`), lo que obliga a ejecutar el motor completo para descubrir que la configuración era irresoluble.

Para decidir cómo evolucionar el motor, se realiza este benchmark de las dos herramientas comerciales de referencia en el mercado español de horarios educativos y se contrasta con la literatura académica de *school timetabling*.

---

## Benchmark de productos de referencia

### Penalara GHC

Producto de referencia en España para generación de horarios de centros educativos (primaria, secundaria, FP, universitario).  
Fuente: [penalara.com/es/queesghc](https://www.penalara.com/es/queesghc) · [penalara.com/blog](https://www.penalara.com/blog/index.php/penalara-software/)

**Arquitectura de tres componentes:**

| Componente | Función |
|---|---|
| **Planificador** | Define condiciones estrictas (hard) y flexibles (soft), importa datos, gestiona disponibilidades y relaciones entre sesiones |
| **Motor GHC Optimum** | Busca automáticamente un compromiso óptimo y equitativo según criterios ponderables; incluye un *analizador de condiciones* que **depura restricciones imposibles antes de lanzar la generación** |
| **Editor** | Drag & drop con avisos de conflicto integrados; asistencia con IA para buscar combinaciones óptimas de movimientos |

**Tres objetivos de optimización (ponderables):**
1. Maximizar el rendimiento académico del alumnado (materias exigentes en franjas de mayor atención)
2. Optimizar la utilización de aulas y espacios
3. Mejorar la satisfacción y condiciones laborales del profesorado

**Restricciones y criterios pedagógicos configurables:**
- Materias de alta carga cognitiva (Matemáticas, Lengua, Inglés) en las primeras horas del día
- Evitar sesiones consecutivas de alta demanda intelectual (prevención de fatiga)
- Dispersión de una misma materia a lo largo de la semana (no concentrar todas las sesiones en uno o dos días)
- Alternancia entre sesiones teóricas y prácticas
- Compactación del horario docente (mínimos huecos entre sesiones del mismo día)
- Máximo de sesiones consecutivas configurables por asignatura y por profesor
- Sesiones fraccionadas: slots de 30, 45 o 60 minutos
- Pesos y prioridades asignables a cada criterio — el motor pondera su «compromiso óptimo»

**Característica diferencial clave:** El *analizador de condiciones* detecta antes de generar si la configuración contiene contradicciones o es matemáticamente imposible, y ofrece diagnósticos concretos sobre qué restricción hay que relajar.

---

### Horarium

Generador de horarios web con cobertura multinivel.  
Fuente: [horarium.ai/es/help](https://horarium.ai/es/help)

**Entidades del modelo de dominio:**  
Clases, Profesores, Asignaturas, Lecciones, Divisiones, Edificios, Aulas y Periodos.

**Restricciones configurables por entidad:**

| Entidad | Restricciones |
|---|---|
| **Profesores** | Límite de lecciones/día, min/max días laborables por semana, períodos de no disponibilidad, restricción a días específicos, tolerancia de huecos entre jornadas |
| **Clases (grupos)** | Número min/max de lecciones diarias, hora máxima de finalización de jornada, turnos diferenciados, extensión más allá de la jornada predefinida |
| **Lecciones** | Frecuencia semanal o por día, exclusión de períodos específicos, múltiples profesores simultáneos (co-docencia), aplicación a subgrupos, carácter no semanal (quincenal, etc.) |
| **Espacios** | Tiempo de traslado entre edificios como restricción de distancia; aulas disponibles por lección/asignatura/clase/profesor |

**16 reglas de optimización**, entre las más relevantes:
- Distribución uniforme de lecciones a lo largo de la semana
- Agrupación en bloques consecutivos
- Minimizar huecos en el horario del docente
- Minimizar el número de días trabajados del docente
- Restricciones de traslado entre sedes/edificios
- Lecciones no semanales (no encaja en el modelo de slot semanal fijo)

**Característica diferencial clave:** **Validación previa** antes de lanzar el motor: detecta aulas insuficientes, restricciones incompatibles entre entidades, y conflictos de configuración en divisiones. Equivale al *analizador de condiciones* de Penalara, pero más formalizado como paso obligatorio.

---

### Teoría de timetabling: estado del arte

El *school timetabling* es un problema **NP-duro** de satisfacción de restricciones (CSP). La literatura académica distingue dos grandes enfoques:

**Enfoques exactos:**
- Programación entera (ILP / MIP): óptimo garantizado, pero escala muy mal (>100 sesiones → impracticable)
- CSP con *constraint propagation*: viable para instancias pequeñas; OR-Tools / Choco son solvers industriales

**Metaheurísticas (dominantes en producción):**

| Técnica | Descripción | Referencia |
|---|---|---|
| **Simulated Annealing (SA)** | Búsqueda local que acepta movimientos que empeoran la función objetivo con probabilidad decreciente (temperatura); escapa de mínimos locales | Abramson 1991 (Management Science) |
| **Tabu Search (TS)** | Búsqueda local con memoria de movimientos recientes prohibidos; especialmente efectivo en timetabling de secundaria | Colorni et al., ResearchGate |
| **ILS (Iterated Local Search)** | Perturbación + búsqueda local repetida | — |
| **VLNS (Very Large Neighborhood Search)** | Vecindarios muy grandes explorados por subproblemas exactos | — |
| **Algoritmos genéticos** | Población de soluciones, cruce y mutación; convergencia lenta pero robusta | Academia.edu |

**Patrón dominante en literatura y productos: arquitectura two-phase**

```
Fase 1 (construcción) → Obtener una solución FACTIBLE (todas las hard constraints satisfechas)
Fase 2 (optimización) → Mejorar las SOFT constraints sin violar ninguna hard,
                        mediante búsqueda local sobre vecindarios (swap/move de slots)
```

La separación es clave: en la fase 1 se puede usar backtracking, greedy o construcción heurística; en la fase 2 se opera sobre una solución ya válida y se aplica SA, TS o hill-climbing sobre movimientos elementales (mover una sesión de slot, intercambiar dos sesiones, etc.).

---

## Mapeo: competencia → motor actual de Lectivo

Estado de cada capacidad en `BacktrackingScheduleEngine` + `Constraints.cs`:

| Capacidad (Penalara / Horarium) | Estado en Lectivo | Ubicación en código |
|---|---|---|
| Profesor no duplicado en el mismo slot | ✅ Cubierta | `TeacherNotDoubleBooked` |
| Aula no duplicada en el mismo slot | ✅ Cubierta | `ClassroomNotDoubleBooked` |
| Disponibilidad declarada del profesor | ✅ Cubierta | `TeacherAvailabilityConstraint` |
| Máximo de horas semanales por profesor | ✅ Cubierta | `MaxWeeklyHoursConstraint` |
| Máximo de sesiones consecutivas por asignatura | ✅ Cubierta | `MaxConsecutiveSlotsConstraint` |
| Especialidad requerida para la asignatura | ✅ Cubierta | `RequiresSpecialistConstraint` |
| Materias intensivas fuera de la última hora | ✅ Cubierta (soft, peso 7) | `NoIntensiveSubjectLastSlot` |
| Dispersión de asignatura a lo largo de la semana | ✅ Cubierta (soft, peso 5) | `DistributeSubjectAcrossDays` |
| Minimizar huecos en el horario docente | ✅ Cubierta (soft, peso 4) | `TeacherGapsConstraint` |
| Bloques consecutivos indivisibles | ✅ Cubierta (soft, peso 5) | `ConsecutiveBlockPreferenceConstraint` |
| Carga consecutiva máxima del profesor | ✅ Cubierta (soft, peso 6) | `TeacherConsecutiveLoadConstraint` |
| Pesos/prioridades configurables por criterio | ⚠️ Parcial | `ISoftConstraint.Weight` existe pero los valores son hardcoded por clase; no se expone al `GenerationContext` |
| **Optimización global post-factibilidad (two-phase)** | ❌ Gap | El motor para en la primera solución completa (`result.Count == sessions.Count`); no hay fase 2 |
| **Función objetivo global explícita** | ❌ Gap | Solo se evalúa la penalización local de cada candidato, no el coste total del horario |
| **Analizador de viabilidad previo (pre-flight)** | ❌ Gap | Los conflictos solo se generan *a posteriori* en `BuildConflictExplanations` |
| Sesiones fraccionadas (30 / 45 min) | ❌ Gap | El modelo asume slots de duración uniforme (1 slot = 1 sesión) |
| Lecciones multi-profesor / co-docencia | ❌ Gap | `SessionToAssign` tiene exactamente 1 `TeacherId` |
| Mín/máx días laborables del docente | ❌ Gap | No hay restricción de tipo «no trabajes más de 4 días» |
| Traslado entre edificios / sedes | ❌ Gap | No existe el concepto de edificio en el modelo |
| Subgrupos dentro de una clase | ❌ Gap | `GroupId` es atómico; no hay divisiones |
| Lecciones no semanales (quincenales, etc.) | ❌ Gap | Se asume recurrencia semanal fija |

---

## Decisión

Adoptar una **arquitectura two-phase** para el motor de generación, con dos mejoras previas que la habilitan:

1. **Análisis de viabilidad previo (pre-flight)**: nueva etapa antes de `Backtrack()` que valida capacidad agregada y emite `ConflictExplanation` accionables si la configuración es irresoluble, sin necesidad de ejecutar el motor completo.

2. **Función objetivo global y pesos configurables**: definir una función `int ComputeScheduleCost(IReadOnlyList<AssignedSlot> schedule)` que sume las penalizaciones de todas las soft constraints sobre el horario completo. Los pesos se exponen en `GenerationContext` para que cada colegio los personalice.

3. **Two-phase**: mantener el backtracking actual como **fase 1** (constructor de solución factible), y añadir una **fase 2** de búsqueda local que mejore la función objetivo global sin violar ninguna hard constraint, usando movimientos elementales (move-slot, swap-slots).

Los gaps de modelo (sesiones fraccionadas, co-docencia, edificios, subgrupos) se documentan como **backlog futuro (v2+)** y no forman parte de esta decisión.

---

## Motivo

1. **El backtracking ya funciona como constructor**: produce horarios válidos (0 violaciones de hard) de forma confiable. No hay razón para reemplazarlo — solo para añadirle una segunda fase.
2. **La calidad del horario importa comercialmente**: un horario donde el profesor tiene 3 huecos diarios o Matemáticas siempre cae a última hora es técnicamente válido pero pedagógicamente malo. Sin fase 2, la calidad queda a merced del orden de asignación.
3. **La pre-validación reduce fricción**: descubrir que la configuración es imposible *después* de 30 segundos de CPU es una mala experiencia. Penalara y Horarium la hacen *antes*.
4. **Los pesos configurables son un diferencial de producto**: que cada colegio pueda ponderar «prefiero horario compacto para el profesor» vs. «prefiero dispersión de materias» es una feature de valor.
5. **Two-phase es el estándar del sector**: tanto la literatura (SA, TS, ILS sobre solución factible) como los productos comerciales siguen este patrón; no hay razón para inventar algo diferente.

---

## Alternativas consideradas

### A. Quedarse en backtracking puro (sin cambios)

- **Pro**: sin deuda técnica, el motor ya funciona para el piloto.
- **Contra**: la calidad de los horarios generados es subóptima y no mejora con el tiempo; imposible competir con Penalara a medio plazo.
- **Descartada** para el motor productivo; válida como baseline de pruebas.

### B. CSP con solver externo (OR-Tools / Google Constraint Programming)

- **Pro**: OR-Tools incluye propagadores altamente optimizados; garantía de optimalidad en instancias pequeñas.
- **Contra**: introduce una dependencia externa pesada (.NET binding de OR-Tools); la curva de aprendizaje del modelo CP-SAT es significativa; para instancias reales (>300 sesiones) el solver exacto también necesita metaheurísticas o búsqueda local.
- **Descartada** en esta iteración; puede reconsiderarse si la fase 2 no alcanza la calidad requerida.

### C. Programación entera (ILP/MIP)

- **Pro**: óptimo garantizado.
- **Contra**: no escala; instancias de colegios reales tienen miles de variables binarias; tiempos de resolución de minutos/horas. No viable para un producto web con timeout de 30 s.
- **Descartada**.

### D. Algoritmo genético (GA) puro

- **Pro**: exploración global del espacio; paralelizable.
- **Contra**: requiere operadores de cruce/mutación específicos para timetabling (respetar hard constraints es difícil con cruce estándar); convergencia lenta; más complejo de implementar y depurar que búsqueda local.
- **Descartada** para v1; puede usarse como alternativa a SA en la fase 2 si esta demuestra insuficiente diversidad.

### E. Simulated Annealing como motor completo (reemplazar backtracking)

- **Pro**: robusto, bien estudiado para timetabling.
- **Contra**: SA como constructor desde cero requiere manejar violaciones de hard constraints durante la búsqueda (penalización en la función objetivo), lo que complica el diseño y puede producir soluciones parcialmente inválidas.
- **Descartada**: mejor usar backtracking para construir una solución factible y SA solo en la fase de mejora (two-phase).

---

## Consecuencias y roadmap

### Fase inmediata (v1.1) — refactor del motor existente

| # | Cambio | Tipo | Impacto en código |
|---|---|---|---|
| 1 | **Pre-flight / analizador de viabilidad** | Nueva clase `ScheduleViabilityAnalyzer` | Reutiliza `ConflictExplanation`; se llama antes de `GenerateAsync` |
| 2 | **Pesos configurables** | Añadir `SoftConstraintWeights` a `GenerationContext` | `ISoftConstraint.Penalty()` recibe los pesos; los valores hardcoded pasan a defaults |
| 3 | **Función objetivo global** | Nuevo método en `BacktrackingScheduleEngine` | `int ComputeScheduleCost(assignments, softConstraints)` |

### Fase siguiente (v2.0) — two-phase

| # | Cambio | Tipo | Impacto en código |
|---|---|---|---|
| 4 | **Fase 2: búsqueda local** | Nueva clase `LocalSearchOptimizer` (hill-climbing o SA) | Recibe la solución factible de la fase 1 y aplica movimientos `move-slot` / `swap-slots` |
| 5 | **Vecindario de movimientos** | Definir `IScheduleMove` con `MoveSlot` y `SwapSlots` | Cada movimiento verifica hard constraints antes de aplicarse |

### Backlog de modelo (v3+, sin fecha comprometida)

- Sesiones fraccionadas (slots de duración variable)
- Co-docencia (multi-profesor en una lección)
- Edificios y tiempo de traslado
- Min/max días laborables del docente
- Subgrupos y divisiones dentro de un grupo
- Lecciones no semanales

---

## Fuentes

- [Penalara GHC — ¿Qué es GHC?](https://www.penalara.com/es/queesghc)
- [Penalara Software Blog](https://www.penalara.com/blog/index.php/penalara-software/)
- [Horarium — Ayuda](https://horarium.ai/es/help)
- [GHC: genera automáticamente los mejores horarios — Educación 3.0](https://www.educaciontrespuntocero.com/recursos/ghc-penalara/)
- Abramson, D. (1991). *Constructing School Timetables Using Simulated Annealing: Sequential and Parallel Algorithms*. Management Science, 37(1). [DOI:10.1287/mnsc.37.1.98](https://pubsonline.informs.org/doi/10.1287/mnsc.37.1.98)
- Colorni et al. *Tabu Search Techniques for Large High-School Timetabling Problems*. [ResearchGate](https://www.researchgate.net/publication/3411974_Tabu_Search_Techniques_for_Large_High-School_Timetabling_Problems)
- *A local search for the timetabling problem*. [ResearchGate](https://www.researchgate.net/publication/228954166_A_local_search_for_the_timetabling_problem)
- *Metaheuristics for High School Timetabling*. Computational Optimization and Applications. [Springer](https://link.springer.com/article/10.1023/A:1018354324992)
- *Mathematical models and algorithms for a high school timetabling problem*. [ScienceDirect](https://www.sciencedirect.com/science/article/abs/pii/S0305054815000428)
- *Constraint programming approach for school timetabling*. [ResearchGate](https://www.researchgate.net/publication/222891149_Constraint_programming_approach_for_school_timetabling)
