# SPEC-002: Scaffolding .NET 8 API + Railway + Dockerfile

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 0 — Infraestructura  
**Estimación:** 1 día  
**Depende de:** —  

---

## Contexto

Base del backend. Debe quedar configurado con .NET 8 Minimal API, Vertical Slice Architecture, Swagger, health check, CORS para el frontend y desplegado en Railway vía Docker.

---

## Criterios de aceptación

**ESCENARIO 1: Health check operativo**
```
Given: La API está desplegada en Railway
When:  GET https://api.horarioscolegios.es/health
Then:  Responde 200 OK con body { "status": "healthy" }
And:   Responde en menos de 500ms
```

**ESCENARIO 2: CORS configurado**
```
Given: El frontend Angular está en https://horarioscolegios.es
When:  Realiza una petición a la API
Then:  No hay error de CORS
And:   Solo están permitidos los orígenes configurados en variables de entorno
```

**ESCENARIO 3: Swagger disponible en desarrollo**
```
Given: La API corre en entorno Development
When:  GET http://localhost:5000/swagger
Then:  Se muestra la UI de Swagger con todos los endpoints documentados
And:   En producción (Railway), /swagger devuelve 404
```

**ESCENARIO 4: Deploy automático en Railway**
```
Given: El Dockerfile está en src/api/
When:  Se hace push a main
Then:  Railway detecta el cambio y redespliega automáticamente
And:   El deploy completa sin errores
And:   La variable de entorno ASPNETCORE_ENVIRONMENT=Production está activa
```

**ESCENARIO 5: Estructura Vertical Slice**
```
Given: Se añade una feature nueva
When:  Se sigue la convención de slices
Then:  Existe un directorio Features/{NombreFeature}/ con:
       - {Feature}Endpoint.cs
       - {Feature}Command.cs o {Feature}Query.cs  
       - {Feature}Handler.cs
       - {Feature}Dto.cs (si aplica)
```

---

## Estructura de ficheros resultante

```
src/api/
├── Domain/
│   ├── Entities/
│   │   ├── School.cs
│   │   ├── Teacher.cs
│   │   ├── CourseGroup.cs
│   │   ├── Classroom.cs
│   │   ├── Schedule.cs
│   │   └── ScheduleEntry.cs
│   ├── ValueObjects/
│   │   ├── SlotTime.cs
│   │   ├── WeeklyAllocation.cs
│   │   └── ConflictExplanation.cs
│   ├── Constraints/
│   │   ├── IHardConstraint.cs
│   │   └── ISoftConstraint.cs
│   └── Services/
│       └── IScheduleEngine.cs
│
├── Features/
│   ├── Schedules/
│   │   ├── Generate/
│   │   │   ├── GenerateScheduleEndpoint.cs
│   │   │   ├── GenerateScheduleCommand.cs
│   │   │   └── GenerateScheduleHandler.cs
│   │   └── GetMySchedule/
│   │       ├── GetMyScheduleEndpoint.cs
│   │       ├── GetMyScheduleQuery.cs
│   │       └── GetMyScheduleHandler.cs
│   └── Schools/
│       └── Create/
│           ├── CreateSchoolEndpoint.cs
│           ├── CreateSchoolCommand.cs
│           └── CreateSchoolHandler.cs
│
├── Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   └── Repositories/
│   ├── Engine/
│   │   └── BacktrackingScheduleEngine.cs
│   └── Auth/
│       └── SupabaseJwtHandler.cs
│
├── Hubs/
│   └── GenerationProgressHub.cs
│
├── Program.cs
├── appsettings.json
├── appsettings.Development.json       # en .gitignore
├── appsettings.Development.example.json  # plantilla sin secretos
├── Dockerfile
└── railway.toml
```

---

## Configuración clave

### Program.cs — estructura base
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // necesario para SignalR
    });
});

// Registrar features
builder.Services.AddScoped<IScheduleEngine, BacktrackingScheduleEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// Endpoints por feature
app.MapScheduleEndpoints();
app.MapSchoolEndpoints();

app.MapHub<GenerationProgressHub>("/hubs/generation");
app.MapHealthChecks("/health");

app.Run();
```

### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "HorariosEscolares.API.dll"]
```

### railway.toml
```toml
[build]
builder = "DOCKERFILE"
dockerfilePath = "src/api/Dockerfile"

[deploy]
startCommand = "dotnet HorariosEscolares.API.dll"
healthcheckPath = "/health"
healthcheckTimeout = 30
restartPolicyType = "ON_FAILURE"
restartPolicyMaxRetries = 3
```

---

## Variables de entorno requeridas (Railway)

```
ASPNETCORE_ENVIRONMENT=Production
Supabase__Url=https://xxx.supabase.co
Supabase__AnonKey=xxx
Supabase__JwtSecret=xxx
AllowedOrigins__0=https://horarioscolegios.es
AllowedOrigins__1=https://www.horarioscolegios.es
```

---

## Fuera de alcance

- Implementación de features reales
- Configuración de EF Core con Supabase (SPEC-003)
- Autenticación JWT completa (SPEC-011)
