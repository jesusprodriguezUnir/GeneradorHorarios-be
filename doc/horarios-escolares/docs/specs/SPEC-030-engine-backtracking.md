# SPEC-030: Motor de generación — Backtracking v1

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 3 — Motor de generación  
**Estimación:** 5 días  
**Depende de:** SPEC-003, SPEC-025, SPEC-026  

---

## Contexto

El motor de generación es el núcleo diferencial del producto. Recibe la configuración completa de un colegio y genera el horario semanal cumpliendo todas las restricciones hard y optimizando las soft. Usa backtracking con propagación de constraints. La generación es asíncrona con progreso en tiempo real vía SignalR.

---

## Algoritmo

```
1. Ordenar sesiones por dificultad de asignación (más restringidas primero)
   - Especialistas (Música, Ed. Física, PT) → primero
   - Asignaturas con aula específica (Gimnasio) → segundo
   - Resto por número de horas descendente

2. Para cada sesión sin asignar:
   a. Obtener slots candidatos (día × franja)
   b. Filtrar por hard constraints
   c. Ordenar por soft constraints (mejor puntuación primero)
   d. Asignar el mejor slot
   e. Si no hay candidatos → backtrack al slot anterior

3. Si timeout (30s por defecto) → devolver mejor solución parcial
4. Generar ConflictExplanation para cada sesión no asignada
```

---

## Criterios de aceptación

**ESCENARIO 1: Generación completa sin conflictos**
```
Given: Un colegio con 6 grupos, 8 profesores, configuración estándar
And:   Todas las asignaciones profesor→asignatura→grupo completas
And:   Horas semanales dentro del rango LOMLOE
When:  Se invoca GenerateScheduleAsync
Then:  Devuelve ScheduleResult con Status = Complete
And:   Todas las sesiones asignadas (0 conflictos)
And:   Ningún profesor aparece en dos grupos al mismo tiempo
And:   Ningún aula tiene dos grupos al mismo tiempo
And:   Las horas semanales de cada asignatura son exactamente las configuradas
And:   Completa en menos de 30 segundos
```

**ESCENARIO 2: Generación con conflictos — solución parcial**
```
Given: Un colegio donde es imposible asignar todas las sesiones
       (ej: especialista con más horas que slots disponibles)
When:  Se invoca GenerateScheduleAsync con timeout 30s
Then:  Devuelve ScheduleResult con Status = Partial
And:   Las sesiones asignadas NO tienen conflictos entre sí
And:   Cada sesión no asignada tiene un ConflictExplanation
And:   ConflictExplanation incluye tipo, descripción y sugerencias
```

**ESCENARIO 3: Progreso en tiempo real**
```
Given: La generación está en curso
When:  El cliente está suscrito al hub de SignalR
Then:  Recibe GenerationProgressEvent cada vez que se asigna una sesión
And:   El evento incluye: sesiones_asignadas, sesiones_totales, porcentaje
And:   El evento final indica si fue Complete o Partial
```

**ESCENARIO 4: Timeout controlado**
```
Given: Un caso extremadamente complejo (18 grupos, muchas restricciones)
When:  La generación alcanza el timeout configurado
Then:  El motor para limpiamente (no se cuelga el proceso)
And:   Devuelve la mejor solución parcial encontrada hasta ese momento
And:   No devuelve una excepción no controlada
```

**ESCENARIO 5: Hard constraint — no solapamiento profesor**
```
Given: Ana García imparte Matemáticas a 1ºA y 2ºA
When:  El motor genera el horario
Then:  Nunca asigna Matemáticas de 1ºA y 2ºA al mismo día y franja
And:   Si no puede evitarlo → registra ConflictExplanation de tipo Teacher
```

---

## Interfaz del dominio

```csharp
public interface IScheduleEngine
{
    Task<ScheduleResult> GenerateAsync(
        GenerationContext context,
        CancellationToken cancellationToken,
        IProgress<GenerationProgress>? progress = null);
}

public record GenerationContext
{
    public required SchoolConfiguration School { get; init; }
    public required IReadOnlyList<Assignment> Assignments { get; init; }
    public required IReadOnlyList<IHardConstraint> HardConstraints { get; init; }
    public required IReadOnlyList<ISoftConstraint> SoftConstraints { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
}

public record ScheduleResult
{
    public GenerationStatus Status { get; init; }  // Complete | Partial | Failed
    public required IReadOnlyList<ScheduleEntry> Entries { get; init; }
    public required IReadOnlyList<ConflictExplanation> Conflicts { get; init; }
    public int ElapsedSeconds { get; init; }
    public int TotalSessionsAssigned { get; init; }
    public int TotalSessionsRequired { get; init; }
}

public record GenerationProgress
{
    public int Assigned { get; init; }
    public int Total { get; init; }
    public int Percentage => Total == 0 ? 0 : (Assigned * 100 / Total);
    public string CurrentAction { get; init; } = string.Empty;
}
```

---

## Fuera de alcance

- OR-Tools (v2)
- Generación multi-semana o por quincenas
- Intercambio automático de sesiones para resolver conflictos
- Aprendizaje de preferencias entre ejecuciones
