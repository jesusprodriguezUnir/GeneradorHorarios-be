# Lectivo — HorariosEscolares

> Generación automática y optimizada de horarios para centros de educación infantil y primaria bajo la normativa LOMLOE de la Comunidad de Madrid.

**Lectivo** es una plataforma diseñada para resolver el problema NP-duro del *school timetabling* (planificación de horarios escolares) de manera óptima y automatizada, reduciendo el proceso de diseño manual que suele tardar semanas a una ejecución inteligente de menos de 15 minutos.

---

## 🚀 Stack Tecnológico

| Capa | Tecnología |
|---|---|
| **Backend** | .NET 8 · C# · Vertical Slice Architecture (Carter) · SignalR Hubs · EF Core 8 |
| **Base de Datos** | SQL Server 2022 (Docker) · Soporte nativo para Multi-Tenancy y campos JSON |
| **Frontend** | [GeneradorHorarios-spa](https://github.com/jesusprodriguezUnir/GeneradorHorarios-spa) (Repo separado) — Angular 21 · Zoneless · Signals · PrimeNG Aura |

---

## 📦 Estructura del Repositorio

El backend y la lógica central de negocio están organizados bajo un monorepo que sigue principios de **Clean Architecture** para el núcleo y **Vertical Slices** para la capa de API web:

```
GeneradorHorarios-be/
├── apps/
│   └── api/                  # Proyecto Web API .NET 8 (Vertical Slices)
│       ├── BackgroundJobs/   # Trabajos asíncronos en segundo plano
│       ├── Features/         # Slices: Auth, Teachers, Classrooms, Schedules, etc.
│       └── Program.cs        # Punto de entrada de la API y registro de Carter/SignalR
├── src/                      # Capas del dominio reutilizables
│   ├── HorariosEscolares.Domain/         # Entidades, Restricciones y abstracciones de servicios
│   ├── HorariosEscolares.Application/    # Casos de uso e interfaces de persistencia
│   └── HorariosEscolares.Infrastructure/ # Persistencia (EF Core) + Motor Backtracking
├── tests/                    # Suites de pruebas automáticas
│   └── api/
│       ├── Lectivo.UnitTests/            # Pruebas unitarias de constraints y motor (~1s)
│       └── Lectivo.IntegrationTests/     # Pruebas de integración sobre Docker (Testcontainers)
├── doc/                      # Documentación, especificaciones y manuales
│   ├── arquitectura.md       # Guía detallada de la arquitectura del sistema
│   ├── manual_usuario.md     # Manual detallado de la aplicación en Markdown
│   ├── manual_usuario.pdf    # Manual de usuario maquetado para PDF con estilos corporativos
│   └── database-diagram.md   # Diagrama e información de la base de datos
├── scripts/                  # Scripts de utilidad para base de datos y compilación
│   ├── reset-db.ps1          # Recreación completa del esquema de BD
│   ├── seed-dataset.ps1      # Sembrado de datos demo LOMLOE
│   └── generate_pdf_manual.py # Compilador de Markdown a PDF corporativo
├── docker-compose.yml        # Orquestación del contenedor de SQL Server 2022
└── README.md
```

---

## 🛠️ Inicio Rápido (Desarrollo)

### 1. Levantar la Base de Datos con Docker
Inicia el contenedor de SQL Server:
```bash
docker compose up -d
```
Espera aproximadamente 15 segundos hasta que el healthcheck pase (`docker compose ps` muestre el contenedor como `healthy`).

### 2. Arrancar la API del Backend
```bash
cd apps/api
dotnet run
```
La API aplicará de manera automática las migraciones pendientes de EF Core y sembrará los datos iniciales de prueba si la base de datos está vacía.
* **Swagger UI** (Documentación de API): [http://localhost:5000/swagger](http://localhost:5000/swagger)
* **Hub de SignalR**: `/hubs/generation`

### 3. Arrancar el Frontend (Repositorio Separado)
Clona la SPA de Angular y sigue sus instrucciones en:
* [GeneradorHorarios-spa](https://github.com/jesusprodriguezUnir/GeneradorHorarios-spa)

---

## 👥 Cuentas de Demostración

El sistema incluye autenticación simulada utilizando el header `X-User-Email` en cada petición HTTP:

| Correo Electrónico | Rol / Permisos | Uso en el flujo demo |
|---|---|---|
| `elena.castro@ceip-miguel-hernandez.es` | **Jefatura de Estudios** (Admin) | Administra profesores, aulas, grupos y genera el horario global del centro. |
| `laura.fernandez@ceip-miguel-hernandez.es` | **Docente** (Teacher) | Visualiza su horario personal optimizado para móviles. |

---

## 🔄 Flujo de Trabajo E2E (Demostración)
1. Inicia sesión en la app Angular como **Elena Castro** (Jefatura de Estudios).
2. Entra en **Configuración** y revisa los recursos del CEIP Miguel Hernández (8 profesores, 6 grupos, asignaturas oficiales LOMLOE).
3. Dirígete a **Generador** y recorre el asistente de 4 pasos (Diagnóstico -> Exclusiones del profesor -> Restricciones de aula -> Generar).
4. Presiona **Generar Horario** y observa el progreso asíncrono en tiempo real alimentado por SignalR.
5. Revisa los resultados o posibles conflictos en la rejilla, ajusta alguna sesión mediante **Drag & Drop** y pulsa **Publicar Horario**.
6. Cambia de perfil en la parte superior a **Laura Fernández** (Profesora).
7. Accede a **Mi Horario** para ver las clases asignadas a Laura en formato escritorio o móvil.

---

## 📑 Documentación Técnica y de Usuario

Hemos estructurado y generado guías detalladas para el mantenimiento del sistema:

* **Guía de Arquitectura del Sistema**: [doc/arquitectura.md](file:///d:/Proyectos/Horarios/GeneradorHorarios-be/doc/arquitectura.md)
  - Detalla la división Clean Architecture + Vertical Slices.
  - Explica las entrañas del motor `BacktrackingScheduleEngine` (heurísticas fail-first, restricciones duras/blandas y pre-check de viabilidad).
  - Describe las medidas de seguridad para multi-tenancy y aislamiento cross-tenant.
* **Manual de Usuario (Markdown)**: [doc/manual_usuario.md](file:///d:/Proyectos/Horarios/GeneradorHorarios-be/doc/manual_usuario.md)
  - Guía completa de uso funcional de Lectivo paso a paso para directores y profesores.
* **Manual de Usuario (PDF Corporativo)**: [doc/manual_usuario.pdf](file:///d:/Proyectos/Horarios/GeneradorHorarios-be/doc/manual_usuario.pdf)
  - Versión oficial en formato PDF estilizada con la paleta de colores de Lectivo (Navy, Teal, Gold).

### Cómo compilar de nuevo el PDF del Manual
Si editas el contenido de [doc/manual_usuario.md](file:///d:/Proyectos/Horarios/GeneradorHorarios-be/doc/manual_usuario.md), puedes regenerar el PDF oficial con el diseño corporativo ejecutando:
```bash
python scripts/generate_pdf_manual.py
```
*Requisitos: Python 3.x con las librerías `reportlab` y `markdown-it-py` instaladas (`pip install reportlab markdown-it-py`).*

---

## 🛢️ Credenciales y Overrides de Base de Datos

* **Servidor**: `localhost,1433` (Docker local)
* **Usuario**: `sa`
* **Contraseña**: `Lectivo_Dev_2024!`
* **Base de Datos**: `LectivoDb`

### Overrides Locales
Si necesitas conectar la API a otra instancia de base de datos (como SQL Server LocalDB o una base de datos hospedada), edita [apps/api/appsettings.Local.json](file:///d:/Proyectos/Horarios/GeneradorHorarios-be/apps/api/appsettings.Local.json) o configura la variable de entorno:

**En PowerShell:**
```powershell
$env:ConnectionStrings__Default = "Server=localhost,1433;Database=LectivoDb;User Id=sa;Password=TuPassword!;TrustServerCertificate=True;"
dotnet run --project apps/api
```

### Scripts de Utilidad (PowerShell)
* **Recrear base de datos y esquema**:
  ```powershell
  .\scripts\reset-db.ps1
  ```
* **Sembrar datos de prueba oficiales LOMLOE Madrid**:
  ```powershell
  .\scripts\seed-dataset.ps1
  ```

---

## 🧪 Pruebas Automatizadas

El proyecto cuenta con una cobertura de pruebas estructurada en dos niveles:

### Pruebas Unitarias (Lógica de Constraints y Motor)
```bash
dotnet test tests/api/Lectivo.UnitTests
```
*Ejecuta más de 100 pruebas en ~1 segundo verificando que cada regla LOMLOE e inviabilidad se comporte de acuerdo con las especificaciones.*

### Pruebas de Integración (Base de Datos e Infraestructura)
```bash
dotnet test tests/api/Lectivo.IntegrationTests
```
*Hace uso de **Testcontainers** para levantar un contenedor ligero de SQL Server al vuelo y validar la API de forma aislada.*
