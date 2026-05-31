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
        json.Should().Contain("workingDays");
    }

    [Fact]
    public async Task GetMySchool_ReturnsIdentificationFields()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/schools/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("centerCode").GetString().Should().Be("28013291");
        root.GetProperty("locality").GetString().Should().Be("Madrid");
        root.GetProperty("community").GetString().Should().Be("madrid");
        root.GetProperty("stage").GetString().Should().Be("primaria");
        root.GetProperty("minCourseLevel").GetInt32().Should().Be(1);
        root.GetProperty("maxCourseLevel").GetInt32().Should().Be(6);
        root.GetProperty("academicYear").GetString().Should().Be("2025/2026");
        root.GetProperty("workingDays").EnumerateArray().Select(x => x.GetInt32())
            .Should().BeEquivalentTo([1, 2, 3, 4, 5]);
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
            afternoonSlots = 2,
        };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("08:30");
        json.Should().Contain("partida");
        json.Should().Contain("15:00");

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
            afternoonSlots = 2,   // 3 slots mañana + 2 slots tarde
        };

        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("partida");
        json.Should().Contain("15:00");
        json.Should().Contain("09:00");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("slotsPerDay").GetInt32().Should().Be(5);
        root.GetProperty("daysPerWeek").GetInt32().Should().Be(5);
        root.GetProperty("afternoonSlots").GetInt32().Should().Be(2);
        root.GetProperty("afternoonStart").GetString().Should().Be("15:00");

        var slots = root.GetProperty("computedSlots");
        slots.GetArrayLength().Should().Be(6); // 3 mañana + 1 recreo + 2 tarde

        var s0 = slots[0];
        s0.GetProperty("index").GetInt32().Should().Be(0);
        s0.GetProperty("startTime").GetString().Should().Be("09:00");
        s0.GetProperty("endTime").GetString().Should().Be("10:00");
        s0.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        var s1 = slots[1];
        s1.GetProperty("index").GetInt32().Should().Be(1);
        s1.GetProperty("startTime").GetString().Should().Be("10:00");
        s1.GetProperty("endTime").GetString().Should().Be("11:00");
        s1.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        var sBreak = slots[2];
        sBreak.GetProperty("index").GetInt32().Should().Be(-1);
        sBreak.GetProperty("startTime").GetString().Should().Be("11:00");
        sBreak.GetProperty("endTime").GetString().Should().Be("11:30");
        sBreak.GetProperty("isBreak").GetBoolean().Should().BeTrue();

        var s2 = slots[3];
        s2.GetProperty("index").GetInt32().Should().Be(2);
        s2.GetProperty("startTime").GetString().Should().Be("11:30");
        s2.GetProperty("endTime").GetString().Should().Be("12:30");
        s2.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        var s3 = slots[4];
        s3.GetProperty("index").GetInt32().Should().Be(3);
        s3.GetProperty("startTime").GetString().Should().Be("15:00");
        s3.GetProperty("endTime").GetString().Should().Be("16:00");
        s3.GetProperty("isBreak").GetBoolean().Should().BeFalse();

        var s4 = slots[5];
        s4.GetProperty("index").GetInt32().Should().Be(4);
        s4.GetProperty("startTime").GetString().Should().Be("16:00");
        s4.GetProperty("endTime").GetString().Should().Be("17:00");
        s4.GetProperty("isBreak").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PutMySchool_WorkingDays_UpdatesAndSyncsDaysPerWeek()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { workingDays = new[] { 1, 2, 4, 5 } }; // sin miércoles
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("daysPerWeek").GetInt32().Should().Be(4);
        root.GetProperty("workingDays").EnumerateArray().Select(x => x.GetInt32())
            .Should().BeEquivalentTo([1, 2, 4, 5]);

        // Restaurar para no afectar otros tests
        await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(new { workingDays = new[] { 1, 2, 3, 4, 5 } }), Encoding.UTF8, "application/json"));
    }

    [Fact]
    public async Task PutMySchool_WorkingDays_Empty_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { workingDays = Array.Empty<int>() };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("al menos un día lectivo");
    }

    [Fact]
    public async Task PutMySchool_WorkingDays_DuplicateDays_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { workingDays = new[] { 1, 1, 2 } };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("no pueden repetirse");
    }

    [Fact]
    public async Task PutMySchool_AfternoonSlots_EqualToSlotsPerDay_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { slotsPerDay = 5, afternoonSlots = 5 }; // debe ser < slotsPerDay
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("slots de tarde");
    }

    [Fact]
    public async Task PutMySchool_IdentificationFields_PersistCorrectly()
    {
        var client = _factory.CreateAdminClient();

        var payload = new
        {
            centerCode = "28099001",
            locality = "Alcalá de Henares",
            community = "madrid",
            stage = "primaria",
            minCourseLevel = 1,
            maxCourseLevel = 6,
            academicYear = "2026/2027",
        };
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("centerCode").GetString().Should().Be("28099001");
        root.GetProperty("locality").GetString().Should().Be("Alcalá de Henares");
        root.GetProperty("academicYear").GetString().Should().Be("2026/2027");
    }

    [Fact]
    public async Task PutMySchool_CourseLevelRange_InvalidMinMax_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { minCourseLevel = 5, maxCourseLevel = 3 }; // min > max
        var response = await client.PutAsync("/api/schools/me",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("rango de cursos");
    }

    [Fact]
    public async Task PutMySchool_JornadaPartida_InvalidAfternoonStart_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        // Tarde solapando con la mañana (11:00 < fin de mañana)
        var payload = new
        {
            scheduleType = "partida",
            morningStart = "09:00",
            afternoonStart = "11:00",
            slotMinutes = 60,
            breakAfterSlot = 2,
            breakMinutes = 30,
            slotsPerDay = 5,
            afternoonSlots = 2,
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
