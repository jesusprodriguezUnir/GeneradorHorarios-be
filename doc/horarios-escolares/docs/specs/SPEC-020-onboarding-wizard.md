# SPEC-020: Onboarding wizard (15 minutos)

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 2 — Configuración del colegio  
**Estimación:** 3 días  
**Depende de:** SPEC-010, SPEC-011, SPEC-024  

---

## Contexto

El onboarding es el momento de mayor riesgo de abandono. Un director que no puede generar su primer horario en 15 minutos no vuelve. El wizard guía paso a paso con valores razonables por defecto, minimizando las decisiones obligatorias.

---

## Flujo del wizard (4 pasos)

```
Paso 1: Estructura del colegio
  → ¿Cuántos cursos tienes? (1º a 6º — selector visual)
  → ¿Cuántos grupos por curso? (A, B, C...)
  → Genera automáticamente los grupos (ej: 1ºA, 1ºB, 2ºA...)

Paso 2: Jornada y horario
  → ¿Jornada continua o partida? (radio visual)
  → Hora de entrada (default: 09:00)
  → Duración de cada sesión (45 / 50 / 60 min — selector)
  → Recreo: tras qué sesión y cuántos minutos (default: tras 2ª, 30 min)
  → Preview del horario diario generado automáticamente

Paso 3: Profesores
  → Importar desde CSV/Excel (recomendado)
  → O añadir manualmente (nombre + email + especialidad)
  → Mínimo 1 profesor para continuar
  → Badge de progreso: "X profesores añadidos"

Paso 4: Asignaturas
  → "Usamos la plantilla LOMLOE Madrid 2024, ¿la ajustamos?"
  → Muestra las asignaturas con horas por defecto
  → Toggle para ajustar horas (dentro del rango normativo)
  → Botón final: "Generar mi primer horario →"
```

---

## Criterios de aceptación

**ESCENARIO 1: Completar wizard mínimo viable**
```
Given: Un director acaba de registrar su colegio
When:  Completa los 4 pasos con datos básicos (3 cursos, 1 grupo, 2 profesores)
Then:  El wizard tarda menos de 15 minutos en completarse
And:   Al finalizar se lanza automáticamente la generación del horario
And:   El director llega a la pantalla de resultado con un horario generado
```

**ESCENARIO 2: Navegación entre pasos**
```
Given: El director está en el paso 3
When:  Pulsa "Anterior"
Then:  Vuelve al paso 2 con los datos que introdujo conservados
And:   Puede modificarlos y avanzar de nuevo
```

**ESCENARIO 3: Validación por paso**
```
Given: El director está en el paso 1 sin seleccionar ningún curso
When:  Pulsa "Siguiente"
Then:  No avanza y ve el mensaje "Selecciona al menos un curso"
And:   No hay validación global hasta el paso 4
```

**ESCENARIO 4: Import CSV profesores**
```
Given: El director sube un CSV con columnas: nombre, email, especialidad
When:  El sistema procesa el fichero
Then:  Los profesores aparecen listados con sus datos
And:   Los emails inválidos se marcan en rojo con mensaje claro
And:   Los profesores válidos se mantienen aunque haya errores en otros
```

**ESCENARIO 5: Plantilla LOMLOE precargada**
```
Given: El director llega al paso 4
When:  Ve las asignaturas
Then:  Todas las asignaturas LOMLOE Madrid están precargadas con horas por defecto
And:   Puede modificar las horas dentro del rango normativo (min-max)
And:   Si intenta poner horas fuera del rango ve un warning con la normativa
And:   El badge "Plantilla oficial Madrid" es visible
```

**ESCENARIO 6: Retomar wizard interrumpido**
```
Given: El director completó el paso 2 y cerró la app
When:  Vuelve a entrar
Then:  Ve el wizard en el paso 3 (donde lo dejó)
And:   Los datos de los pasos anteriores están conservados
```

---

## Fuera de alcance

- Import desde otros sistemas (Alexia, Gescola) — v2
- Configuración de aulas en el wizard (se hace después en Configuración)
- Restricciones de profesores en el wizard (se hace en perfil del profesor)
