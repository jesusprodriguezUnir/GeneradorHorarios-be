# CONSTITUCIÓN — HorariosEscolares

**Versión:** 1.1  
**Fecha:** Mayo 2025  
**Estado:** ACTIVA  

> Documento inamovible. Requiere consenso explícito y entrada en el [changelog](#changelog) para cualquier modificación.

---

## ARTÍCULO 1 — PROPÓSITO Y ALCANCE

HorariosEscolares es una aplicación web PWA para generación automática y gestión de horarios en colegios de primaria, inicialmente bajo normativa de la Comunidad de Madrid (LOMLOE).

### Usuarios objetivo (por orden de prioridad)

| Rol | Descripción |
|-----|-------------|
| `school_admin` | Jefatura de estudios / Director — genera y gestiona horarios |
| `teacher` | Profesor — consulta su horario personal |
| `super_admin` | Administrador técnico — acceso global (solo desarrollo) |

### Fuera de alcance v1

- Secundaria, Bachillerato, FP
- Otras comunidades autónomas (v2)
- Gestión de notas o faltas de asistencia de alumnos
- Comunicación familia-colegio
- Facturación / pagos

---

## ARTÍCULO 2 — PRINCIPIOS DE PRODUCTO

### P1 · Onboarding en 15 minutos
Un colegio nuevo debe poder generar su primer horario en menos de 15 minutos desde el registro.  
→ Cualquier feature que añada fricción al onboarding requiere justificación explícita.

### P2 · Explicabilidad sobre perfección
Un horario con conflictos explicados es mejor que un horario perfecto sin explicación.  
→ El motor **siempre** explica por qué no pudo asignar algo. Nunca devuelve un error genérico.

### P3 · Mobile first sin degradación
La experiencia del profesor en móvil es ciudadana de primera clase, no un afterthought.  
→ Toda pantalla se diseña primero en 390px.

### P4 · Normativa como dato, no como código
Las horas por asignatura, decretos y plantillas oficiales son registros en base de datos, editables.  
→ Cambios normativos = cambio de datos, no de código.

### P5 · Cero pérdida de datos
Un horario publicado **nunca** se sobreescribe automáticamente. Toda nueva generación es una versión nueva. El histórico es sagrado.

### P6 · Feedback inmediato
Ninguna acción del usuario queda sin respuesta visual en menos de 200ms.  
→ Si una operación tarda más de 2s, hay indicador de progreso obligatorio.

---

## ARTÍCULO 3 — DECISIONES DE ARQUITECTURA

### 3.1 Stack — INAMOVIBLE en v1

| Capa | Tecnología | Motivo |
|------|-----------|--------|
| Frontend | **Angular 20** · Zoneless · Signals | Stack del equipo, Zoneless production-ready en v20, Signals + effect() estables |
| Backend | .NET 8 · Minimal API · Vertical Slice | Stack del equipo, OR-Tools disponible, SignalR nativo |
| Base de datos | Supabase (PostgreSQL + Auth + RLS) | Auth + RLS + free tier — sin gestión de infraestructura |
| Hosting FE | Vercel | CI/CD, CDN global, dominio custom, SSL automático |
| Hosting BE | Railway | Docker, free tier con crédito, .NET support nativo |
| DNS / CDN | Cloudflare | Registrar a precio de coste + proxy + DDoS protection |
| UI Components | PrimeNG | FullCalendar, DataTable, Forms — mejor cobertura que Material para este dominio |

### 3.2 Restricciones de Angular 20

```
✅ Zoneless habilitado desde el inicio (bootstrapApplication con provideExperimentalZonelessChangeDetection)
✅ Signals para todo estado reactivo (schedule grid, progreso de generación)
✅ effect() estable — usar sin restricciones
✅ computed() para derivaciones del estado del horario
✅ Standalone components — ningún NgModule
✅ Reactive Forms clásicos para formularios de configuración
❌ Signal-based forms — NO usar, aún experimental en v20
❌ Zone.js — NO incluir en el proyecto
❌ NgModules — NO usar en código nuevo
```

### 3.3 Arquitectura de la API

**Patrón:** Vertical Slice Architecture

```
→ Cada feature es un slice independiente
→ No se comparte lógica entre slices salvo dominio core
→ Un slice = Command/Query + Handler + Endpoint + Tests
```

**No usar en v1:**
- MediatR (añade complejidad sin retorno en proyectos unipersonales)
- Repositorio genérico / Unit of Work manual
- CQRS con buses separados

### 3.4 Motor de generación

**Estrategia:** Backtracking con constraint propagation

```
→ Timeout configurable (default: 30s)
→ Si no hay solución perfecta en timeout:
   devuelve mejor solución parcial + lista de conflictos explicados
→ OR-Tools como upgrade en v2, no antes
→ NUNCA bloquear el hilo HTTP durante la generación
→ Siempre async con SignalR para progreso en tiempo real
```

### 3.5 Autenticación

**Proveedor:** Supabase Auth (JWT)

```
→ Magic link (email) como método principal
→ Google OAuth como alternativa
→ NO username/password en v1
```

**Roles:**

| Rol | Permisos |
|-----|----------|
| `school_admin` | CRUD completo de su colegio |
| `teacher` | Lectura de su horario únicamente |
| `super_admin` | Acceso a todos los colegios (solo dev) |

### 3.6 Multi-tenancy

**Modelo:** Schema compartido con `school_id` en cada tabla  
→ Row Level Security en Supabase garantiza aislamiento  
→ Un usuario pertenece a exactamente un colegio en v1

### 3.7 Versionado de horarios

**Regla:** Inmutabilidad de horarios publicados

```
Estados: Draft → Generated → Published → Archived

→ Solo puede existir UN horario Published por colegio
→ Publicar uno archiva el anterior automáticamente
→ Los datos del horario Published son read-only en BD
→ Toda nueva generación crea un Schedule nuevo en estado Draft
```

---

## ARTÍCULO 4 — ESTÁNDARES DE CALIDAD

### 4.1 Testing

**Obligatorio antes de merge:**

```
→ Tests unitarios del motor de generación: cobertura > 90%
→ Tests unitarios de constraints: 100% (son reglas de negocio críticas)
→ Tests de integración en endpoints críticos:
   - POST /schedules/generate
   - GET /schedules/me
   - POST /schools/{id}/publish
```

**No obligatorio en v1:**
- E2E (Playwright) — añadir en v2
- Tests de componentes Angular

### 4.2 Contratos de rendimiento

| Operación | Objetivo (p95) |
|-----------|---------------|
| `GET /schedules/me` | < 300ms |
| Generación colegio pequeño (≤6 grupos) | < 30s |
| Generación colegio mediano (≤18 grupos) | < 120s |
| Carga inicial Angular (4G) | < 3s |
| LCP en móvil | < 2.5s |
| Respuesta visual a acción de usuario | < 200ms |

### 4.3 Estándares de código

```
C#:
  → Nullable enabled, warnings as errors
  → Record types para Value Objects y DTOs
  → Nombres en inglés

Angular:
  → Strict mode habilitado
  → Standalone components únicamente
  → Signals para estado, no BehaviorSubject donde sea posible
  → Nombres en inglés en código, español en UI y templates

General:
  → Sin comentarios obvios; el código se autodocumenta
  → Commits: Conventional Commits (feat/fix/chore/docs/test)
  → Sin console.log en producción
```

### 4.4 Seguridad

```
→ Toda petición autenticada valida school_id del JWT
→ RLS en Supabase como segunda línea de defensa
→ Sin datos personales de profesores en logs
→ HTTPS obligatorio en todos los entornos
→ Variables de entorno para todas las credenciales — nunca en código
```

---

## ARTÍCULO 5 — GESTIÓN DE CAMBIOS

### Para modificar esta constitución

1. Propuesta documentada con motivo y alternativas consideradas
2. Revisión de impacto en specs y features existentes
3. Decisión explícita registrada en el [changelog](#changelog)
4. Actualización de specs afectadas **antes** de codificar

### No requiere modificar la constitución

- Añadir features nuevas (van a Feature Specs)
- Cambios de UI / UX
- Optimizaciones de rendimiento dentro de los contratos
- Bugfixes

---

## CHANGELOG

| Versión | Fecha | Cambio |
|---------|-------|--------|
| 1.0 | Mayo 2025 | Versión inicial |
| 1.1 | Mayo 2025 | Angular 18 → Angular 20. Zoneless production-ready. Signals + effect() estables. Restricciones de uso de Signal-based forms añadidas. |
