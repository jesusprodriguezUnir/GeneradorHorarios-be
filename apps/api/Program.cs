using Microsoft.EntityFrameworkCore;
using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Features.Schools;
using HorariosEscolares.Features.Teachers;
using HorariosEscolares.Features.Groups;
using HorariosEscolares.Features.Classrooms;
using HorariosEscolares.Features.Subjects;
using HorariosEscolares.Features.Assignments;
using HorariosEscolares.Features.Constraints;
using HorariosEscolares.Features.Schedules;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ─────────────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Lectivo API", Version = "v1" });
    c.AddSecurityDefinition("X-User-Email", new()
    {
        Name = "X-User-Email",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Email del usuario demo (auth simulada)",
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "X-User-Email" } },
            []
        }
    });
});

builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("Default");
    options.UseSqlServer(conn, sql =>
    {
        sql.EnableRetryOnFailure(3);
    });
});

// Motor de generación de horarios
builder.Services.AddScoped<IScheduleEngine, BacktrackingScheduleEngine>();

var app = builder.Build();

// ── Inicialización de BD (migraciones + seed) ─────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.InitializeAsync(db);
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lectivo API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("Frontend");
app.UseMiddleware<DevAuthMiddleware>();   // auth simulada — reemplazar por JWT en producción

// ── Endpoints por feature ─────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapSchoolEndpoints();
app.MapTeacherEndpoints();
app.MapGroupEndpoints();
app.MapClassroomEndpoints();
app.MapSubjectEndpoints();
app.MapAssignmentEndpoints();
app.MapConstraintEndpoints();
app.MapScheduleEndpoints();

app.MapHub<GenerationProgressHub>("/hubs/generation");
app.MapHealthChecks("/health");

// Redirect root to swagger en dev
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

public partial class Program { }
