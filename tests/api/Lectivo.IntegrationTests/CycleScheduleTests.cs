using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Lectivo.IntegrationTests;

/// <summary>
/// Tests de integración para la configuración de horarios de ciclos.
/// Verifica los endpoints PUT /api/schools/me/periods/{periodId}/cycles/{cycle}
/// y el de compatibilidad PUT /api/schools/me/cycles/{cycle}.
/// </summary>
[Collection("MsSql collection")]
public class CycleScheduleTests
{
    private readonly LectivoApiFactory _factory;

    public CycleScheduleTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static StringContent Json(object payload)
        => new(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Encoding.UTF8, "application/json");

    private async Task<Guid> GetDefaultPeriodIdAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/schools/me/stages");
        resp.EnsureSuccessStatusCode();
        using var stagesDoc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var stageId = stagesDoc.RootElement[0].GetProperty("id").GetString()!;

        var periodsResp = await client.GetAsync($"/api/schools/me/periods?stageId={stageId}");
        periodsResp.EnsureSuccessStatusCode();
        using var periodsDoc = JsonDocument.Parse(await periodsResp.Content.ReadAsStringAsync());
        // El periodo ordinario (isDefault=true) es el primero ordenado
        var defaultPeriod = periodsDoc.RootElement.EnumerateArray()
            .First(p => p.GetProperty("isDefault").GetBoolean());
        return Guid.Parse(defaultPeriod.GetProperty("id").GetString()!);
    }

    // ── Jornada continua ─────────────────────────────────────────────────────

    [Fact]
    public async Task PutPeriodCycle_JornadaContinua_DevuelveLas4Horas()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new
        {
            morningStart = "09:00",
            morningEnd   = "14:00",
        };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/1", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("morningStart").GetString().Should().Be("09:00");
        root.GetProperty("morningEnd").GetString().Should().Be("14:00");
        root.TryGetProperty("afternoonStart", out var asEl);
        (asEl.ValueKind == JsonValueKind.Null || asEl.ValueKind == JsonValueKind.Undefined)
            .Should().BeTrue("jornada continua no debe tener tarde");
        // ComputedSlots debe contener 5 franjas de 60 min (sin recreo en este payload)
        var slots = root.GetProperty("computedSlots").EnumerateArray()
            .Where(s => !s.GetProperty("isBreak").GetBoolean()).ToList();
        slots.Should().HaveCountGreaterOrEqualTo(5);
    }

    [Fact]
    public async Task GetPeriodCycle_DespuesDePut_DevuelveHorasGuardadas()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new
        {
            morningStart = "08:45",
            morningEnd   = "13:45",
        };
        await client.PutAsync($"/api/schools/me/periods/{periodId}/cycles/2", Json(payload));

        var resp = await client.GetAsync($"/api/schools/me/periods/{periodId}/cycles/2");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("morningStart").GetString().Should().Be("08:45");
        doc.RootElement.GetProperty("morningEnd").GetString().Should().Be("13:45");
        doc.RootElement.GetProperty("endTime").GetString().Should().Be("13:45");
    }

    // ── Jornada partida ──────────────────────────────────────────────────────

    [Fact]
    public async Task PutPeriodCycle_JornadaPartida_DevuelveLas4Horas()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new
        {
            morningStart   = "09:00",
            morningEnd     = "13:00",
            afternoonStart = "15:00",
            afternoonEnd   = "17:00",
        };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/3", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("morningStart").GetString().Should().Be("09:00");
        root.GetProperty("morningEnd").GetString().Should().Be("13:00");
        root.GetProperty("afternoonStart").GetString().Should().Be("15:00");
        root.GetProperty("afternoonEnd").GetString().Should().Be("17:00");
        root.GetProperty("endTime").GetString().Should().Be("17:00");
    }

    [Fact]
    public async Task PutPeriodCycle_JornadaPartida_ComputedSlotsContieneMananaYTarde()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new
        {
            morningStart   = "09:00",
            morningEnd     = "12:00",   // 3 franjas de 60 min mañana
            afternoonStart = "15:00",
            afternoonEnd   = "17:00",   // 2 franjas de 60 min tarde
        };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/1", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var lectivos = doc.RootElement.GetProperty("computedSlots").EnumerateArray()
            .Where(s => !s.GetProperty("isBreak").GetBoolean()).ToList();
        lectivos.Should().HaveCount(5); // 3 mañana + 2 tarde
        lectivos[0].GetProperty("startTime").GetString().Should().Be("09:00");
        lectivos[3].GetProperty("startTime").GetString().Should().Be("15:00");
        lectivos[4].GetProperty("endTime").GetString().Should().Be("17:00");
    }

    // ── Validaciones → 400 ──────────────────────────────────────────────────

    [Fact]
    public async Task PutPeriodCycle_MorningStartGeEnd_Devuelve400()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new { morningStart = "14:00", morningEnd = "09:00" };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/1", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutPeriodCycle_AfternoonStartSinEnd_Devuelve400()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new
        {
            morningStart   = "09:00",
            morningEnd     = "13:00",
            afternoonStart = "15:00",
            // AfternoonEnd omitido → inválido
        };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/2", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutPeriodCycle_AfternoonStartAntesDeMananaEnd_Devuelve400()
    {
        var client = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(client);

        var payload = new
        {
            morningStart   = "09:00",
            morningEnd     = "14:00",
            afternoonStart = "13:00",  // solapado con mañana
            afternoonEnd   = "16:00",
        };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/1", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Autorización ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PutPeriodCycle_NoAdmin_Devuelve403()
    {
        var client = _factory.CreateTeacherClient();
        var adminClient = _factory.CreateAdminClient();
        var periodId = await GetDefaultPeriodIdAsync(adminClient);

        var payload = new { morningStart = "09:00", morningEnd = "14:00" };

        var resp = await client.PutAsync(
            $"/api/schools/me/periods/{periodId}/cycles/1", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Endpoint de compatibilidad (/me/cycles/{cycle}) ──────────────────────

    [Fact]
    public async Task PutMyCycles_CompatEndpoint_DevuelveLas4Horas()
    {
        var client = _factory.CreateAdminClient();

        var payload = new
        {
            morningStart = "09:00",
            morningEnd   = "14:00",
        };

        var resp = await client.PutAsync("/api/schools/me/cycles/1", Json(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("morningEnd").GetString().Should().Be("14:00");
    }
}
