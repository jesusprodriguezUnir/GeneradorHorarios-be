using HorariosEscolares.Domain.Services;
using HorariosEscolares.Infrastructure.Engine;
using HorariosEscolares.Features.Schedules;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ─────────────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "HorariosEscolares API", Version = "v1" });
});

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
              .AllowCredentials();
    });
});

// Dominio
builder.Services.AddScoped<IScheduleEngine, BacktrackingScheduleEngine>();

// Supabase / EF (se configura en SPEC-003)
// builder.Services.AddDbContext<AppDbContext>(...);

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints por feature (Vertical Slice) ────────────────────────────────────

app.MapScheduleEndpoints();

app.MapHub<GenerationProgressHub>("/hubs/generation");
app.MapHealthChecks("/health");

app.Run();
