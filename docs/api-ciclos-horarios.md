# Contrato API — Configuración de horarios de ciclos

> **Versión:** 2.0 · Migración `AddCycleSessionBoundaries`  
> Este documento describe los endpoints y DTOs para editar la jornada de cada ciclo desde la pantalla de ciclos del SPA.

---

## Modelo de jornada por ciclo

Cada ciclo guarda **4 horas** que delimitan su jornada lectiva:

| Campo | Tipo | Descripción |
|---|---|---|
| `morningStart` | `string HH:mm` | Entrada mañana |
| `morningEnd` | `string HH:mm` | Salida mañana |
| `afternoonStart` | `string HH:mm \| null` | Entrada tarde (`null` = jornada continua) |
| `afternoonEnd` | `string HH:mm \| null` | Salida tarde (`null` si no hay tarde) |
| `endTime` | `string HH:mm` | **Solo lectura.** Igual a `afternoonEnd ?? morningEnd` |
| `computedSlots` | `SlotDto[]` | **Solo lectura.** Franjas calculadas a partir de las 4 horas |
| `breaks` | `CycleBreakDto[]` | Recreos (ver abajo) |

Las franjas (`computedSlots`) se derivan automáticamente de las 4 horas + la duración de franja del periodo (`slotMinutes`). No se envían en el PUT.

---

## Endpoint principal

### `GET /api/schools/me/periods/{periodId}/cycles/{cycle}`
Devuelve la configuración de un ciclo.

**Response 200:**
```json
{
  "cycle": 1,
  "morningStart": "09:00",
  "morningEnd": "14:00",
  "afternoonStart": null,
  "afternoonEnd": null,
  "endTime": "14:00",
  "computedSlots": [
    { "index": 0, "startTime": "09:00", "endTime": "10:00", "isBreak": false },
    { "index": 1, "startTime": "10:00", "endTime": "11:00", "isBreak": false },
    { "index": -1, "startTime": "11:00", "endTime": "11:30", "isBreak": true },
    { "index": 2, "startTime": "11:30", "endTime": "12:30", "isBreak": false },
    { "index": 3, "startTime": "12:30", "endTime": "13:30", "isBreak": false },
    { "index": 4, "startTime": "13:30", "endTime": "14:30", "isBreak": false }
  ],
  "breaks": [
    { "afterSlot": 2, "minutes": 30 }
  ]
}
```

**Response 404:** el ciclo aún no tiene configuración propia.

---

### `PUT /api/schools/me/periods/{periodId}/cycles/{cycle}`
Crea o actualiza la jornada de un ciclo. Requiere rol **Admin** (`X-User-Email` de director/jefe de estudios/secretario).

#### Cuerpo — jornada continua
```json
{
  "morningStart": "09:00",
  "morningEnd": "14:00",
  "breaks": [
    { "afterSlot": 2, "minutes": 30 }
  ]
}
```

#### Cuerpo — jornada partida
```json
{
  "morningStart": "09:00",
  "morningEnd": "13:00",
  "afternoonStart": "15:00",
  "afternoonEnd": "17:00",
  "breaks": [
    { "afterSlot": 2, "minutes": 30 }
  ]
}
```

**Campos opcionales:**
- `afternoonStart` / `afternoonEnd` — se omiten en jornada continua.
- `breaks` — si se omite, se conservan los recreos actuales. Para borrar todos los recreos, enviar `"breaks": []`.

**Response 200:** mismo esquema que el GET.  
**Response 400:** error de validación (ver reglas abajo).  
**Response 403:** usuario sin permisos de administrador.

---

## Endpoint de compatibilidad (periodo por defecto)

Apunta automáticamente al periodo marcado como `isDefault`. Útil cuando solo hay un periodo.

```
GET  /api/schools/me/cycles/{cycle}
PUT  /api/schools/me/cycles/{cycle}
```
Misma interfaz que los endpoints de periodo.

---

## Reglas de validación

| Condición | Error |
|---|---|
| `morningStart >= morningEnd` | 400 — "La entrada de la mañana debe ser anterior a la salida de la mañana." |
| `afternoonStart` presente sin `afternoonEnd` | 400 — "Si se indica inicio de tarde, debe indicarse también la salida de tarde." |
| `afternoonEnd` presente sin `afternoonStart` | 400 — "Si se indica salida de tarde, debe indicarse también el inicio de tarde." |
| `afternoonStart < morningEnd` | 400 — "La entrada de la tarde no puede ser anterior a la salida de la mañana." |
| `afternoonStart >= afternoonEnd` | 400 — "La entrada de la tarde debe ser anterior a la salida de la tarde." |

---

## `computedSlots` — cómo se calculan

Las franjas lectivas se derivan con la siguiente lógica:
1. Empezar en `morningStart`.
2. Añadir una franja de `slotMinutes` (del periodo) siempre que quepa antes de `morningEnd`.
3. Si hay un recreo (`breaks`) configurado para el índice actual, insertarlo antes de la siguiente franja.
4. Si hay `afternoonStart`/`afternoonEnd`, continuar igual desde la tarde (numeración de índice continua).

Las franjas con `isBreak: true` tienen `index: -1`. Las lectivas tienen índices `0, 1, 2, …`.  
El campo `endTime` del slot coincide con el inicio del siguiente (incluyendo el recreo).

---

## Cómo obtener el `periodId`

```
GET /api/schools/me/stages              → lista de etapas con su Id
GET /api/schools/me/periods?stageId=X   → periodos de esa etapa
```

El periodo ordinario tiene `"isDefault": true`. Los ciclos de primaria son `1`, `2` y `3`.

---

## Ejemplos completos `curl`

### Leer ciclo 1 del periodo ordinario
```bash
curl -H "X-User-Email: elena.castro@ceip-miguel-hernandez.es" \
  http://localhost:5000/api/schools/me/periods/{periodId}/cycles/1
```

### Guardar jornada continua para el ciclo 2
```bash
curl -X PUT \
  -H "X-User-Email: elena.castro@ceip-miguel-hernandez.es" \
  -H "Content-Type: application/json" \
  -d '{"morningStart":"09:00","morningEnd":"14:00","breaks":[{"afterSlot":2,"minutes":30}]}' \
  http://localhost:5000/api/schools/me/periods/{periodId}/cycles/2
```

### Guardar jornada partida para el ciclo 3
```bash
curl -X PUT \
  -H "X-User-Email: elena.castro@ceip-miguel-hernandez.es" \
  -H "Content-Type: application/json" \
  -d '{
    "morningStart":"09:00",
    "morningEnd":"13:00",
    "afternoonStart":"15:00",
    "afternoonEnd":"17:00",
    "breaks":[{"afterSlot":2,"minutes":30}]
  }' \
  http://localhost:5000/api/schools/me/periods/{periodId}/cycles/3
```

---

## Notas para la implementación del SPA

- La pantalla de ciclos debe mostrar un formulario con **4 campos de hora** por cada ciclo de la etapa seleccionada: *Entrada mañana*, *Salida mañana*, *Entrada tarde*, *Salida tarde*. Los dos últimos se ocultan/deshabilitan cuando la jornada del periodo es "continua".
- Los recreos se pueden gestionar opcionalmente en una sección expandible.
- Al guardar, llamar a `PUT /api/schools/me/periods/{periodId}/cycles/{cycle}` para **cada** ciclo que se haya editado (una llamada por ciclo).
- `computedSlots` y `endTime` son de solo lectura — mostrarlos como previsualización pero nunca enviarlos.
- Para obtener los periodos de una etapa: `GET /api/schools/me/periods?stageId={stageId}`.
