# SPEC-040: "Mi Horario" — Vista del profesor (mobile first)

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 4 — Visualización  
**Estimación:** 3 días  
**Depende de:** SPEC-011, SPEC-036  

---

## Contexto

Es la funcionalidad más usada del día a día. Un profesor entra, ve su horario de la semana directamente, sin configurar nada. Es la killer feature para adopción: si el director lo adopta, todos los profesores lo usan para consultar su horario.

---

## Criterios de aceptación

**ESCENARIO 1: Acceso directo al horario**
```
Given: Ana García (teacher) está autenticada
And:   Hay un horario en estado published para su colegio
When:  Navega a la app
Then:  Ve directamente su horario semanal sin pasos intermedios
And:   Cada celda muestra: nombre asignatura, grupo (ej: 3ºA), aula
And:   La semana actual está visible por defecto
```

**ESCENARIO 2: Sin horario publicado**
```
Given: Ana García está autenticada
And:   No hay horario en estado published
When:  Navega a la app
Then:  Ve un mensaje claro: "El equipo directivo todavía no ha publicado el horario"
And:   No hay error ni pantalla en blanco
```

**ESCENARIO 3: Vista móvil — un día a la vez**
```
Given: Ana accede desde un móvil (390px)
When:  Ve su horario
Then:  Se muestra un día a la vez con tabs o swipe entre días
And:   El día actual está seleccionado por defecto
And:   Cada sesión ocupa el ancho completo de la pantalla
And:   La franja horaria es visible (09:00, 10:00...)
And:   El recreo aparece como separador visual entre sesiones
```

**ESCENARIO 4: Vista escritorio — semana completa**
```
Given: Ana accede desde escritorio (>768px)
When:  Ve su horario
Then:  Se muestra el grid semanal completo (L-V × franjas)
And:   Las celdas vacías (horas libres) son visualmente distintas
And:   El recreo aparece como fila separadora
```

**ESCENARIO 5: Descarga PDF**
```
Given: Ana está viendo su horario
When:  Pulsa "Descargar mi horario"
Then:  Se descarga un PDF con su horario semanal
And:   El PDF incluye nombre del profesor, colegio y curso escolar
And:   El PDF es legible en A4 e imprimible
```

**ESCENARIO 6: Rendimiento**
```
Given: Ana accede desde móvil con conexión 4G
When:  Carga la pantalla de su horario
Then:  El horario es visible en menos de 1 segundo tras el login
And:   No hay flash de contenido vacío
```

---

## Implementación Angular 20

```typescript
// features/my-schedule/my-schedule.component.ts
@Component({
  selector: 'app-my-schedule',
  standalone: true,
  template: `
    @if (schedule()) {
      <app-schedule-grid
        [entries]="schedule()!.entries"
        [slots]="school()!.slots"
        [viewMode]="isMobile() ? 'day' : 'week'"
      />
      <p-button label="Descargar mi horario" (onClick)="downloadPdf()" />
    } @else if (loading()) {
      <app-schedule-skeleton />
    } @else {
      <app-no-schedule-message />
    }
  `
})
export class MyScheduleComponent {
  private scheduleApi = inject(ScheduleApiService);
  private deviceService = inject(DeviceService);

  schedule = signal<TeacherSchedule | null>(null);
  loading = signal(true);
  isMobile = this.deviceService.isMobile;   // computed signal

  scheduleResource = resource({
    loader: () => this.scheduleApi.getMySchedule(),
  });
}
```

---

## API endpoint

```
GET /api/schedules/me
Authorization: Bearer {supabase_jwt}

Response 200:
{
  "teacherName": "Ana García",
  "schoolName": "CEIP...",
  "academicYear": "2025-2026",
  "entries": [
    {
      "dayOfWeek": 1,
      "slotIndex": 0,
      "slotTime": "09:00",
      "subjectName": "Matemáticas",
      "groupLabel": "3ºA",
      "classroomName": "Aula 12"
    }
  ],
  "slots": [
    { "index": 0, "startTime": "09:00", "endTime": "09:50", "isBreak": false },
    { "index": 1, "startTime": "09:50", "endTime": "10:40", "isBreak": false },
    { "index": -1, "startTime": "10:40", "endTime": "11:10", "isBreak": true },
    ...
  ]
}

Response 404:
{ "message": "No hay horario publicado para tu colegio" }
```

---

## Fuera de alcance

- Notificaciones push de cambios en el horario (v2)
- iCal sync con Google Calendar (v2)
- Vista del horario de otros profesores (solo admin — SPEC-042)
