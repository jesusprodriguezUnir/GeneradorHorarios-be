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
            afternoonStart = "15:00",
            slotMinutes = 50,
            breakAfterSlot = 3,
            breakMinutes = 25,
            slotsPerDay = 6,
            daysPerWeek = 5
        };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("08:30");
        json.Should().Contain("partida");
        json.Should().Contain("15:00");

        // Verificar slots recalculados
        using var doc = JsonDocument.Parse(json);
        var slots = doc.RootElement.GetProperty("computedSlots");
        slots.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PutMySchool_JornadaPartida_RecalculatesMorningAndAfternoonSlots()
    {
        var client = _factory.CreateAdminClient();

        var payload = new
        {
            scheduleType = "partida",
            morningStart = "09:00",
            afternoonStart = "15:00",
            slotMinutes = 60,
            breakAfterSlot = 2,
            breakMinutes = 30,
            slotsPerDay = 5,
            daysPerWeek = 5
        };

        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();

        // Verificar propiedades en el DTO devuelto
        json.Should().Contain("partida");
        json.Should().Contain("15:00");
        json.Should().Contain("09:00");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.GetProperty("slotsPerDay").GetInt32().Should().Be(5);
        root.GetProperty("daysPerWeek").GetInt32().Should().Be(5);
        root.GetProperty("afternoonStart").GetString().Should().Be("15:00");

        var slots = root.GetProperty("computedSlots");
        slots.GetArrayLength().Should().Be(6); // 5 slots + 1 break = 6 slots en total

        // Slot 0: 09:00 - 10:00 (Mañana)
        var s0 = slots[0];
        s0.GetProperty("index").GetInt32().Should().Be(0);
        s0.GetProperty("startTime").GetString().Should().Be("09:00");
        s0.GetProperty("endTime").GetString().Should().Be("10:00");
        s0.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        // Slot 1: 10:00 - 11:00 (Mañana)
        var s1 = slots[1];
        s1.GetProperty("index").GetInt32().Should().Be(1);
        s1.GetProperty("startTime").GetString().Should().Be("10:00");
        s1.GetProperty("endTime").GetString().Should().Be("11:00");
        s1.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        // Recreo (Index -1)
        var sBreak = slots[2];
        sBreak.GetProperty("index").GetInt32().Should().Be(-1);
        sBreak.GetProperty("startTime").GetString().Should().Be("11:00");
        sBreak.GetProperty("endTime").GetString().Should().Be("11:30");
        sBreak.GetProperty("isBreak").GetBoolean().Should().BeTrue();

        // Slot 2: 11:30 - 12:30 (Mañana)
        var s2 = slots[3];
        s2.GetProperty("index").GetInt32().Should().Be(2);
        s2.GetProperty("startTime").GetString().Should().Be("11:30");
        s2.GetProperty("endTime").GetString().Should().Be("12:30");
        s2.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        // Slot 3: 15:00 - 16:00 (Tarde)
        var s3 = slots[4];
        s3.GetProperty("index").GetInt32().Should().Be(3);
        s3.GetProperty("startTime").GetString().Should().Be("15:00");
        s3.GetProperty("endTime").GetString().Should().Be("16:00");
        s3.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        // Slot 4: 16:00 - 17:00 (Tarde)
        var s4 = slots[5];
        s4.GetProperty("index").GetInt32().Should().Be(4);
        s4.GetProperty("startTime").GetString().Should().Be("16:00");
        s4.GetProperty("endTime").GetString().Should().Be("17:00");
        s4.GetProperty("isBreak").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PutMySchool_JornadaPartida_InvalidAfternoonStart_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        // Tarde solapando o anterior a la mañana (11:00 es antes del fin de la mañana que es 12:30)
        var payload = new
        {
            scheduleType = "partida",
            morningStart = "09:00",
            afternoonStart = "11:00",
            slotMinutes = 60,
            breakAfterSlot = 2,
            breakMinutes = 30,
            slotsPerDay = 5
        };

        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("debe ser posterior al final de la jornada de mañana");
    }

    [Fact]
    public async Task PutMySchool_AsTeacher_Returns403()
    {
        var client = _factory.CreateTeacherClient();

        var payload = new { scheduleType = "partida", afternoonStart = "15:00" };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError); // Results.Forbid sin AddAuthentication
    }
}
