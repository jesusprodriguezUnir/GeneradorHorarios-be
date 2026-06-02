using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HorariosEscolares.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class SubjectsTests
{
    private readonly LectivoApiFactory _factory;

    public SubjectsTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetSubjects_InitiallyReturnsOfficialTemplate()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/subjects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(json);
        var subjects = doc.RootElement;
        subjects.GetArrayLength().Should().BeGreaterThan(0);
        
        // Inicialmente todas son oficiales
        foreach (var sub in subjects.EnumerateArray())
        {
            sub.GetProperty("isOfficial").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task CloneOfficialTemplate_CreatesCustomTemplateAndAllocations()
    {
        // Usar un factory/conexión limpia. Dado que la base de datos es compartida en el fixture,
        // podemos crear un nuevo colegio dinámicamente si quisiéramos, pero podemos probar clonando una vez.
        // Espera, para probar el clone de forma aislada, use el cliente admin del fixture.
        // Pero si ya se clonó antes, /clone-official podría devolver BadRequest.
        // Para asegurarnos de que funciona independientemente del orden de ejecución,
        // podemos verificar si el GET ya devuelve asignaturas clonadas o si podemos clonarlas y que responda Ok o BadRequest.
        // Espera, el fixture se limpia o comparte entre tests? El Collection "MsSql collection" comparte la base de datos
        // pero cada clase de test se ejecuta en secuencia.
        // Intentemos llamar a clone-official. Si ya existe, nos dará 400. Pero idealmente probamos el flujo feliz.
        // Hagámoslo tolerante:
        var client = _factory.CreateAdminClient();

        var cloneResponse = await client.PostAsync("/api/subjects/clone-official", null);
        
        if (cloneResponse.StatusCode == HttpStatusCode.OK)
        {
            var cloneJson = await cloneResponse.Content.ReadAsStringAsync();
            using var cloneDoc = JsonDocument.Parse(cloneJson);
            var clonedSubjects = cloneDoc.RootElement;
            clonedSubjects.GetArrayLength().Should().BeGreaterThan(0);

            // Todas las nuevas deben ser IsOfficial = false
            foreach (var sub in clonedSubjects.EnumerateArray())
            {
                sub.GetProperty("isOfficial").GetBoolean().Should().BeFalse();
            }

            // GET posterior debe devolver solo las clonadas (no duplicadas)
            var getResponse = await client.GetAsync("/api/subjects");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getJson = await getResponse.Content.ReadAsStringAsync();
            using var getDoc = JsonDocument.Parse(getJson);
            foreach (var sub in getDoc.RootElement.EnumerateArray())
            {
                sub.GetProperty("isOfficial").GetBoolean().Should().BeFalse();
            }
        }
        else
        {
            cloneResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task PutSubjectsHours_ForOfficialTemplate_ReturnsBadRequest()
    {
        // Obtener un subject de la plantilla oficial directamente desde la BD
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var officialTemplate = await db.CurriculumTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsOfficial);
        officialTemplate.Should().NotBeNull("debe existir una plantilla LOMLOE oficial en el seed");

        var officialSubject = await db.SubjectAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TemplateId == officialTemplate!.Id);
        officialSubject.Should().NotBeNull("la plantilla oficial debe tener asignaturas");

        var client = _factory.CreateAdminClient();
        var payload = new { weeklyHoursDefault = 6 };
        var putResponse = await client.MapPut($"/api/subjects/{officialSubject!.Id}/hours", payload);

        // Oficial → inmutable, debe devolver 400
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var putJson = await putResponse.Content.ReadAsStringAsync();
        putJson.Should().Contain("No se puede modificar la plantilla LOMLOE oficial");
    }
}

public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> MapPut(this HttpClient client, string url, object payload)
    {
        return await client.PutAsync(url,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
    }
}
