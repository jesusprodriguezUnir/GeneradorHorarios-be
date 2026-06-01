# Jornada Escolar en Educación Primaria (Madrid)

## Marco legal

- **Decreto 94/2025**, de 23 de diciembre (BOCM 26 dic 2025) — regula la jornada
  escolar en centros de Infantil, Primaria y Educación Especial de la CAM.
- **Decreto 61/2022**, de 13 de julio — Artículo 10: "El horario lectivo dedicado
  a la impartición del conjunto de las áreas se fija en un mínimo de 22,5 horas
  semanales, a las que se sumarán un mínimo de 2,5 horas semanales repartidas en
  períodos de recreo diarios de 30 minutos."

## Tipos de jornada

### Jornada continua (la más habitual en Madrid)

- Toda la actividad lectiva se concentra en la mañana.
- Ejemplo típico con 5 tramos de 60 min:
  - 09:00–10:00 (tramo 0)
  - 10:00–11:00 (tramo 1)
  - **RECREO 11:00–11:30** (30 min, no lectivo)
  - 11:30–12:30 (tramo 2)
  - 12:30–13:30 (tramo 3)
  - 13:30–14:30 (tramo 4)
- Total lectivo: 5 h/día × 5 días = **25 h/semana** ✓

Configuración en BD:
```
ScheduleType = "continua"
MorningStart = 09:00
SlotsPerDay = 5
AfternoonSlots = 0
BreakAfterSlot = 2
BreakMinutes = 30
SlotMinutes = 60
```

### Jornada partida

- Actividad lectiva en dos tramos: mañana y tarde, con pausa de comedor.
- Ejemplo con 3 tramos mañana + 2 tarde:
  - Mañana: 09:00–10:00, 10:00–11:00, RECREO 11:00–11:30, 11:30–12:30
  - Tarde:  15:00–16:00, 16:00–17:00
- Total lectivo: (3+2) × 60 min × 5 días = **25 h/semana** ✓

Configuración en BD:
```
ScheduleType = "partida"
MorningStart = 09:00
AfternoonStart = 15:00
SlotsPerDay = 5
AfternoonSlots = 2
BreakAfterSlot = 2
BreakMinutes = 30
SlotMinutes = 60
```

## Autonomía del centro

- Los centros pueden elegir el tipo de jornada y adaptar el horario de inicio/fin.
- El recreo NUNCA puede ser inferior a 30 min/día.
- Las horas lectivas siempre ≥ 22,5 h/semana (sin el recreo).
- El `SlotMinutes` puede variar (50 min, 60 min), ajustando `SlotsPerDay` para
  mantener el mínimo lectivo.

## Cálculo en el código

`SlotCalculator.Compute` en `apps/api/Features/Schools/SchoolsFeature.cs:48`
genera la rejilla horaria completa (con recreo marcado como `IsBreak=true`) a
partir de los campos de la entidad `School`.
