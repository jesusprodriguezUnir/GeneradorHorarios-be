# SPEC-034: Explicación de conflictos en lenguaje natural

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 3 — Motor de generación  
**Estimación:** 2 días  
**Depende de:** SPEC-030, SPEC-031, SPEC-032  

---

## Contexto

Cuando el motor no puede asignar una sesión, genera una explicación en español comprensible para jefatura de estudios (no técnica), con causa concreta y máximo 3 sugerencias accionables. Es el diferenciador principal frente a la competencia.

---

## Criterios de aceptación

**ESCENARIO 1: Conflicto por profesor ocupado**
```
Given: Matemáticas de 3ºA necesita el martes 10:00
And:   Ana García ya imparte Matemáticas a 4ºB ese tramo
When:  El motor no puede asignar el slot
Then:  ConflictExplanation contiene:
       - Type: Teacher
       - Severity: Error
       - Description: "No se puede asignar Matemáticas a 3ºA el martes a las 10:00
                       porque Ana García ya imparte Matemáticas a 4ºB en ese tramo."
       - Suggestions[0]: "Mover Matemáticas de 4ºB a otro tramo libre de Ana García"
       - Suggestions[1]: "Asignar otro profesor de Matemáticas a 3ºA"
       - Suggestions[2]: "Cambiar el tramo de Matemáticas de 3ºA a otro horario"
```

**ESCENARIO 2: Conflicto por aula ocupada**
```
Given: Ed. Física de 2ºA necesita el gimnasio el jueves 09:00
And:   El gimnasio está asignado a 5ºB ese tramo
When:  El motor no puede asignar el slot
Then:  ConflictExplanation contiene:
       - Type: Classroom
       - Severity: Error
       - Description: "El gimnasio no está disponible el jueves a las 09:00
                       (ocupado por Ed. Física de 5ºB)."
       - Suggestions[0]: "Mover Ed. Física de 5ºB a otro tramo"
       - Suggestions[1]: "Cambiar el tramo de Ed. Física de 2ºA"
```

**ESCENARIO 3: Conflicto normativo (horas insuficientes)**
```
Given: Inglés de 1ºA tiene 3h asignadas al finalizar la generación
And:   LOMLOE requiere mínimo 4h semanales para Inglés en 1º
When:  El motor finaliza con horas insuficientes
Then:  ConflictExplanation contiene:
       - Type: Normative
       - Severity: Error
       - Description: "Inglés en 1ºA tiene 3 horas semanales asignadas.
                       El mínimo según LOMLOE Madrid es 4 horas."
       - Suggestions[0]: "Revisar la disponibilidad de la profesora de Inglés"
       - Suggestions[1]: "Asignar un segundo profesor de Inglés para 1ºA"
       - Suggestions[2]: "Reducir horas de otra asignatura flexible para liberar tramos"
```

**ESCENARIO 4: Generación sin conflictos**
```
Given: El motor asigna todas las sesiones
When:  Finaliza la generación
Then:  ScheduleResult.Conflicts está vacío
And:   ScheduleResult.Status = Complete
```

**ESCENARIO 5: Ordenación por severidad**
```
Given: La generación produce 5 conflictos mezclados
When:  Se devuelve ScheduleResult
Then:  Los conflictos están ordenados: Normative > Teacher > Classroom > Soft
And:   Dentro del mismo tipo, los de Severity=Error van antes que Warning
```

---

## Value Object

```csharp
public record ConflictExplanation
{
    public required ConflictType Type { get; init; }
    public required ConflictSeverity Severity { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    // Contexto para resaltado visual en el grid
    public Guid? GroupId { get; init; }
    public Guid? TeacherId { get; init; }
    public int? DayOfWeek { get; init; }
    public int? SlotIndex { get; init; }
}

public enum ConflictType { Teacher, Classroom, Normative, Soft }
public enum ConflictSeverity { Error, Warning }
```

---

## Fuera de alcance

- Resolución automática de conflictos
- Traducción a otros idiomas
- Historial de conflictos entre generaciones
