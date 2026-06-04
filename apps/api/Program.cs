using Microsoft.EntityFrameworkCore;
using MediatR;
using HorariosEscolares.Domain.Abstractions;
using HorariosEscolares.Application;
using HorariosEscolares.Infrastructure;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Features.Auth;
using HorariosEscolares.Features.Schools;
using HorariosEscolares.Features.Teachers;
using HorariosEscolares.Features.Groups;
using HorariosEscolares.Features.Classrooms;
using HorariosEscolares.Features.Subjects;
using HorariosEscolares.Features.Assignments;
using HorariosEscolares.Features.Constraints;
using HorariosEscolares.Features.Dev;
using HorariosEscolares.Features.Schedules;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

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

var redisConn = builder.Configuration.GetConnectionString("Redis");
var healthChecksBuilder = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(redisConn))
{
    healthChecksBuilder.AddRedis(redisConn);
}

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
    options.UseSqlServer(conn, sql => sql.EnableRetryOnFailure(3));
});

// Redis / Caché con fallback a memoria
if (!string.IsNullOrWhiteSpace(redisConn))
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
else
    builder.Services.AddDistributedMemoryCache();

// Application layer (MediatR, FluentValidation, behaviors)
builder.Services.AddApplication();

// Infrastructure layer (engine, normative)
builder.Services.AddInfrastructure();

// Register IAppDbContext → AppDbContext (same scoped instance)
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Current user accessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

var app = builder.Build();

// ── Inicialización de BD (migraciones + seed) ─────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedOptions = app.Configuration.GetSection("Seed").Get<SeedOptions>();
    await DbInitializer.InitializeAsync(db, seedOptions);
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseSerilogRequestLogging();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lectivo API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("Frontend");
app.UseMiddleware<DevAuthMiddleware>();

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

if (!app.Environment.IsProduction())
{
    app.MapDevEndpoints();
}
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

public partial class Program { }
