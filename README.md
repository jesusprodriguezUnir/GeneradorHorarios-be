# Lectivo — HorariosEscolares

> Generación automática de horarios para colegios de primaria (LOMLOE Madrid)

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 8 · Minimal API · Vertical Slice · SignalR |
| Base de datos | SQL Server 2022 (Docker) · EF Core 8 |
| Frontend (repo separado) | [GeneradorHorarios-spa](https://github.com/jesusprodriguezUnir/GeneradorHorarios-spa) — Angular 21 · Zoneless · Signals · PrimeNG |

## Inicio rápido

### 1. Levantar SQL Server con Docker

```bash
docker compose up -d
```

Espera ~15 segundos hasta que el healthcheck pase (`docker compose ps` → healthy).

### 2. Arrancar la API

```bash
cd apps/api
dotnet run
```

La API aplica migraciones y siembra datos demo automáticamente.  
Swagger disponible en: **http://localhost:5000/swagger**

### 3. Arrancar el frontend (repo separado)

```bash
# Clona el repo del SPA y sigue su README:
# https://github.com/jesusprodriguezUnir/GeneradorHorarios-spa
```

## Usuarios demo

| Email | Rol |
|-------|-----|
| `elena.castro@ceip-miguel-hernandez.es` | Jefatura de estudios (admin) |
| `laura.fernandez@ceip-miguel-hernandez.es` | Profesora (teacher) |

## Flujo E2E

1. Abre `http://localhost:4200` → selecciona **Elena Castro** (dirección)
2. **Panel** → resumen de KPIs del colegio demo (8 profesores, 6 grupos)
3. **Generador** → recorre los 4 pasos → pulsa **Generar horario**
4. Observa el progreso en tiempo real (SignalR) → llega al **Horario generado**
5. Revisa conflictos, edita una celda, pulsa **Publicar horario**
6. Cambia a **Laura Fernández** (profesora) → **Mi horario** muestra su horario publicado
7. Verifica la vista móvil (DevTools → 390px) → navegación día a día

## Arquitectura

```
GeneradorHorarios-sbe/
├── apps/
│   └── api/          # .NET 8 Vertical Slice
│       ├── Domain/   # Entidades + Constraints + IScheduleEngine
│       ├── Infrastructure/Engine/   # BacktrackingScheduleEngine
│       ├── Infrastructure/Persistence/  # EF Core + DbContext + Seed LOMLOE
│       └── Features/ # Auth/Schools/Teachers/Groups/Classrooms/Subjects/
│                     # Assignments/Constraints/Schedules (slices)
├── tests/
│   └── api/
│       ├── Lectivo.UnitTests/
│       └── Lectivo.IntegrationTests/
├── doc/              # Specs, ADRs, constitución, seed SQL
├── docker-compose.yml
└── README.md
```

## Credenciales Docker (solo desarrollo)

- Server: `localhost,1433`
- User: `sa`
- Password: `Lectivo_Dev_2024!`
- Database: `LectivoDb`

## Conexión local y overrides

Si quieres apuntar la API a tu base de datos local, tienes varias opciones:

- Editar `apps/api/appsettings.Local.json` o `apps/api/appsettings.Development.json` cambiando la clave `ConnectionStrings:Default` al connection string deseado.

	Ejemplo (SQL Server con usuario `sa`):

	```powershell
	Server=localhost,1433;Database=LectivoDb;User Id=sa;Password=TuPassword!;TrustServerCertificate=True;
	```

	Ejemplo (LocalDB / Integrated Security):

	```powershell
	Server=(localdb)\MSSQLLocalDB;Database=LectivoDb;Integrated Security=true;TrustServerCertificate=True;
	```

- Sobrescribir mediante variable de entorno (útil para CI / VS Code):

	PowerShell:

	```powershell
	$env:ConnectionStrings__Default = "Server=localhost,1433;Database=LectivoDb;User Id=sa;Password=TuPassword!;TrustServerCertificate=True;"
	dotnet run --project apps/api
	```

	También puedes editar `apps/api/Properties/launchSettings.json` y cambiar `ConnectionStrings__Default` para el perfil de lanzamiento.

- Aplicar migraciones y sembrar datos:

	```powershell
	cd apps/api
	dotnet ef database update
	# o usar los scripts incluidos
	..\scripts\reset-db.ps1
	..\scripts\seed-dataset.ps1
	```

- Arrancar la API:

	```powershell
	cd apps/api
	dotnet run
	```

Si algo falla al arrancar, copia aquí el error y lo reviso. También puedo intentar arrancar la API ahora y reportarte la salida.
