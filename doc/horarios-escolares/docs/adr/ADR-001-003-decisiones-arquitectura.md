# ADR-001: Angular 20 sobre React para el frontend

**Fecha:** Mayo 2025  
**Estado:** Aceptada  
**Decidido por:** Equipo (proyecto unipersonal)  

---

## Contexto

Elección del framework frontend para una PWA de gestión de horarios escolares desarrollada por un arquitecto .NET con experiencia senior en Angular.

## Decisión

Angular 20 con Zoneless y Signals.

## Motivo

1. Stack del desarrollador → máxima velocidad sin curva de aprendizaje
2. Formularios reactivos complejos → Angular Reactive Forms superiores a React Hook Form para este dominio
3. PWA first-party → `@angular/pwa` sin configuración
4. Zoneless en v20 → production-ready, mejor rendimiento en grids con muchas actualizaciones
5. El riesgo principal del proyecto es el motor de generación, no el frontend → optimizar donde importa

## Alternativas consideradas

- **React + Vite**: mejor ecosistema general, pero requiere 2-3 semanas de adaptación y múltiples decisiones de ecosistema (estado, routing, forms, HTTP)
- **Blazor WASM**: mismo stack .NET, pero el motor de generación necesita API backend de todas formas y la comunidad de UI components es inferior

## Consecuencias

- Zoneless habilitado desde el inicio (no migratable fácilmente después)
- Signal-based forms NO usar hasta que sean estables (post v20)
- NgModules prohibidos — solo standalone components
- Si se necesita hiring futuro, React tiene más mercado

---

# ADR-002: Supabase como backend-as-a-service

**Fecha:** Mayo 2025  
**Estado:** Aceptada  

---

## Contexto

Necesitamos auth, PostgreSQL y aislamiento multi-tenant para el piloto sin infraestructura propia.

## Decisión

Supabase con Row Level Security para aislamiento entre colegios.

## Motivo

1. Free tier suficiente para el piloto (500MB, 50k usuarios)
2. Auth incluida (magic link + OAuth) sin implementar desde cero
3. RLS como segunda línea de defensa — el aislamiento es a nivel de BD, no solo de API
4. PostgreSQL estándar — sin lock-in real, migrable a cualquier Postgres managed

## Alternativas consideradas

- **PostgreSQL propio en Railway**: más control, pero gestión de auth desde cero
- **Firebase**: NoSQL no encaja con el dominio relacional de horarios
- **PlanetScale**: MySQL, sin soporte nativo de arrays y JSONB que necesitamos

## Consecuencias

- Toda la lógica de negocio va en .NET, no en Edge Functions de Supabase
- Las migraciones se gestionan con scripts SQL versionados en `/docs/database/`
- Si superamos el free tier → migrar a Supabase Pro (25$/mes) o PostgreSQL propio

---

# ADR-003: Vertical Slice Architecture en la API

**Fecha:** Mayo 2025  
**Estado:** Aceptada  

---

## Contexto

Elección de patrón arquitectónico para la API .NET en un proyecto unipersonal con dominio complejo.

## Decisión

Vertical Slice Architecture sin MediatR.

## Motivo

1. Cada feature es completamente independiente — fácil de localizar y modificar
2. Sin MediatR en v1 — añade indirección sin retorno en proyectos sin equipo grande
3. El dominio es complejo pero las features son bien delimitadas (generate, view, configure)
4. Los tests son más simples — se testea el handler directamente, no a través de mediator

## Alternativas consideradas

- **Clean Architecture clásica**: capas horizontales generan acoplamiento innecesario entre features no relacionadas
- **CQRS con MediatR**: añade complejidad de configuración para beneficio marginal en este contexto

## Consecuencias

- Algo de duplicación entre features (aceptable y preferible al acoplamiento)
- MediatR puede añadirse en v2 si el equipo crece
- Los endpoints se registran directamente en `Program.cs` mediante extension methods por feature
