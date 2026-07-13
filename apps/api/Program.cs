using Microsoft.EntityFrameworkCore;
using MediatR;
using Hangfire;
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
using HorariosEscolares.Features.Roles;
using HorariosEscolares.Features.Schedules;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ── Servicios ─────────────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Lectivo API", Version = "v1" });
    c.AddSecurityDefinition("X-Api-Key", new()
    {
        Name = "X-Api-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "API key secreta (configurada en ApiKey:Key). Da acceso completo como admin.",
    });
    c.AddSecurityDefinition("X-User-Email", new()
    {
        Name = "X-User-Email",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Email del usuario demo (auth simulada). Alternativa a X-Api-Key.",
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "X-Api-Key" } },
            []
        }
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

// Infrastructure layer (engine, normative, repositories)
builder.Services.AddInfrastructure();

// Hangfire background jobs (SQL Server storage)
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues = ["default", "schedules"];
});

// Register IAppDbContext → AppDbContext (same scoped instance)
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Current user accessor + tenant para los query filters multi-tenant
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();

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
    app.UseHangfireDashboard("/hangfire");
}

app.UseCors("Frontend");

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (UnauthorizedAccessException ex)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (HorariosEscolares.Application.Common.Exceptions.ForbiddenAccessException ex)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (HorariosEscolares.Application.Common.Exceptions.NotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (HorariosEscolares.Application.Common.Exceptions.ValidationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "La solicitud contiene errores de validación.",
            details = ex.Failures.Select(f => new { f.PropertyName, f.ErrorMessage }).ToList(),
        });
    }
    catch (InvalidOperationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
#pragma warning disable CA1848 // Use LoggerMessage delegates - not practical in top-level statements
        logger.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
#pragma warning restore CA1848
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        if (app.Environment.IsDevelopment())
            await context.Response.WriteAsJsonAsync(new { error = ex.Message, type = ex.GetType().Name });
        else
            await context.Response.WriteAsJsonAsync(new { error = "Se ha producido un error interno." });
    }
});

app.UseMiddleware<DevAuthMiddleware>();

// ── Endpoints por feature ─────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapSchoolEndpoints();
app.MapTeacherEndpoints();
app.MapGroupEndpoints();
app.MapClassroomEndpoints();
app.MapSubjectEndpoints();
app.MapAssignmentEndpoints();
app.MapRoleEndpoints();
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
