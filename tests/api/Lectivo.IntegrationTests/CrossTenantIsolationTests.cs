using System.Net;
using FluentAssertions;
using HorariosEscolares.Infrastructure.Persistence;
using HorariosEscolares.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lectivo.IntegrationTests;

/// <summary>
/// Verifica que el admin de un centro no puede leer ni modificar datos de otro centro.
/// Prueba la guarda: if (template.SchoolId != user.SchoolId) return 403.
/// El admin del centro 1 (seed) intenta operar sobre los recursos del centro 2 (inserción propia).
/// </summary>
[Collection("MsSql collection")]
public class CrossTenantIsolationTests : IAsyncLifetime
{
    private readonly LectivoApiFactory _factory;

    // Centro 2 — insertado por este test, IDs fijos para idempotencia
    private static readonly Guid School2Id         = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly Guid School2AdminId    = Guid.Parse("00000000-0000-0000-0000-000000000098");
    private static readonly Guid School2TemplateId = Guid.Parse("00000000-0000-0099-0000-000000000001");
    private static readonly Guid School2SubjectId  = Guid.Parse("00000000-0000-0099-0000-000000000002");

    public CrossTenantIsolationTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Insertar centro 2 completo si no existe (idempotente)
        if (!await db.Schools.AnyAsync(s => s.Id == School2Id))
        {
            db.Schools.Add(new School
            {
                Id   = School2Id,
                Name = "CEIP Prueba Aislamiento",
                Slug = "ceip-prueba-aislamiento",
            });
            db.AppUsers.Add(new AppUser
            {
                Id       = School2AdminId,
                Email    = "admin@ceip-prueba-aislamiento.es",
                FullName = "Admin Otro Centro",
                SchoolId = School2Id,
                Role     = "school_admin",
            });
            db.CurriculumTemplates.Add(new CurriculumTemplate
            {
                Id         = School2TemplateId,
                SchoolId   = School2Id,
                Name       = "Plantilla Aislamiento",
                IsOfficial = false,
            });
            db.SubjectAllocations.Add(new SubjectAllocation
            {
                Id                 = School2SubjectId,
                TemplateId         = School2TemplateId,
                SubjectName        = "Matemáticas (Aislamiento)",
                SubjectShort       = "MAT",
                SubjectKey         = "mat",
                WeeklyHoursMin     = 4,
                WeeklyHoursMax     = 8,
                WeeklyHoursDefault = 5,
                IsOfficial         = false,
            });
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Admin del centro 1 intenta tocar asignaturas del centro 2 ───────────────

    [Fact]
    public async Task PutHours_OnOtherSchoolSubject_ReturnsForbidden()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.MapPut(
            $"/api/subjects/{School2SubjectId}/hours",
            new { weeklyHoursDefault = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutSubject_OnOtherSchoolSubject_ReturnsForbidden()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.MapPut(
            $"/api/subjects/{School2SubjectId}",
            new { subjectName = "Hackeo" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteSubject_OnOtherSchoolSubject_ReturnsForbidden()
    {
        var adminClient = _factory.CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/subjects/{School2SubjectId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Admin del centro 2 intenta tocar asignaturas del centro 2 (verificación positiva) ──

    [Fact]
    public async Task PutHours_OnOwnSchoolSubject_ReturnsOk()
    {
        var school2Client = _factory.CreateClientForUserId(School2AdminId);
        var response = await school2Client.MapPut(
            $"/api/subjects/{School2SubjectId}/hours",
            new { weeklyHoursDefault = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
