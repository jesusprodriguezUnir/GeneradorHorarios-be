# HorariosEscolares

> Generación automática y gestión de horarios para colegios de primaria en España (LOMLOE · Comunidad de Madrid)

[![Angular](https://img.shields.io/badge/Angular-20-red?logo=angular)](https://angular.dev)
[![.NET](https://img.shields.io/badge/.NET-8-purple?logo=dotnet)](https://dotnet.microsoft.com)
[![Supabase](https://img.shields.io/badge/Supabase-PostgreSQL-green?logo=supabase)](https://supabase.com)
[![Vercel](https://img.shields.io/badge/Frontend-Vercel-black?logo=vercel)](https://vercel.com)
[![Railway](https://img.shields.io/badge/Backend-Railway-purple?logo=railway)](https://railway.app)

---

## ¿Qué es esto?

HorariosEscolares permite a jefatura de estudios generar el horario completo de un colegio de primaria en minutos, cumpliendo automáticamente la normativa LOMLOE de la Comunidad de Madrid. Cada profesor puede consultar su horario desde el móvil sin instalar nada.

**Propuesta de valor:**
- ⚡ Primer horario generado en menos de 15 minutos desde el registro
- 🧠 Explicación en lenguaje natural de cada conflicto
- 📱 PWA instalable — profesores ven su horario en móvil
- 📋 Plantilla LOMLOE Madrid precargada y editable
- ⚖️ Detección automática de carga injusta entre profesores

---

## Stack

| Capa | Tecnología |
|------|-----------|
| Frontend | Angular 20 · Zoneless · Signals · PrimeNG · PWA |
| Backend | .NET 8 · Minimal API · Vertical Slice · SignalR |
| Base de datos | Supabase (PostgreSQL + Auth + RLS) |
| Hosting FE | Vercel |
| Hosting BE | Railway |
| DNS / CDN | Cloudflare |

---

## Documentación

- [Constitución del proyecto](docs/constitution/CONSTITUTION.md)
- [Decisiones de arquitectura](docs/adr/)
- [Specs de features](docs/specs/)

---

## Estructura del proyecto

```
horarios-escolares/
├── docs/
│   ├── constitution/     # Principios y decisiones inamovibles
│   ├── specs/            # Feature specs (SDD)
│   └── adr/              # Architecture Decision Records
├── src/
│   ├── api/              # .NET Core 8 API
│   └── web/              # Angular 20 PWA
└── tests/
    ├── Domain.Tests/
    ├── Application.Tests/
    └── Integration.Tests/
```

---

## Inicio rápido (desarrollo local)

### Requisitos
- .NET 8 SDK
- Node.js 20+
- Docker (para Supabase local)

### Backend
```bash
cd src/api
cp appsettings.Development.example.json appsettings.Development.json
# Edita con tus credenciales de Supabase
dotnet run
```

### Frontend
```bash
cd src/web
npm install
cp src/environments/environment.example.ts src/environments/environment.ts
ng serve
```

---

## Fases del proyecto

| Fase | Contenido | Estado |
|------|-----------|--------|
| 0 | Infraestructura y scaffolding | 🔄 En progreso |
| 1 | Auth y estructura base | ⏳ Pendiente |
| 2 | Configuración del colegio | ⏳ Pendiente |
| 3 | Motor de generación | ⏳ Pendiente |
| 4 | Visualización de horarios | ⏳ Pendiente |
| 5 | Exportación | ⏳ Pendiente |
| 6 | Diferenciadores | ⏳ Pendiente |

---

## Licencia

Privado — todos los derechos reservados.
