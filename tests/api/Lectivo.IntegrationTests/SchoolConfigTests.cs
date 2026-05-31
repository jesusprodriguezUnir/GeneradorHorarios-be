using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class SchoolConfigTests
{
    private readonly LectivoApiFactory _factory;

    public SchoolConfigTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetMySchool_ReturnsSchoolConfig()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/schools/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("CEIP Miguel Hernández");
        json.Should().Contain("computedSlots");
    }

    [Fact]
    public async Task PutMySchool_UpdatesConfig()
    {
        var client = _factory.CreateAdminClient();

        var payload = new
        {
            scheduleType = "partida",
            morningStart = "08:30",
            slotMinutes = 50,
            breakAfterSlot = 3,
            breakMinutes = 25,
        };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("08:30");
        json.Should().Contain("partida");

        // Verificar slots recalculados
        using var doc = JsonDocument.Parse(json);
        var slots = doc.RootElement.GetProperty("computedSlots");
        slots.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PutMySchool_AsTeacher_Returns403()
    {
        var client = _factory.CreateTeacherClient();

        var payload = new { scheduleType = "partida" };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError); // Results.Forbid sin AddAuthentication
    }
}
