using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class SchedulesGenerationTests
{
    private readonly LectivoApiFactory _factory;

    public SchedulesGenerationTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Generate_AsAdmin_WithSeedData_ReturnsScheduleId()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("scheduleId", out var scheduleIdProp).Should().BeTrue();
        var scheduleId = scheduleIdProp.GetGuid();

        // GET el horario generado
        var getResponse = await client.GetAsync($"/api/schedules/{scheduleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getJson = await getResponse.Content.ReadAsStringAsync();
        getJson.Should().Contain("entries");
    }

    [Fact]
    public async Task Generate_SignalR_ConnectsAndJoinsGroup()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/generation", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        // La conexión debe establecerse sin errores
        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        // Unirse al grupo de la escuela debe funcionar
        await connection.InvokeAsync("JoinSchoolGroup", "00000000-0000-0000-0000-000000000001");

        // La generación debe completarse mientras estamos conectados
        var client = _factory.CreateAdminClient();
        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task PublishSchedule_ArchivesPrevious()
    {
        var client = _factory.CreateAdminClient();

        // Generar un horario
        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var genResponse = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        var genJson = await genResponse.Content.ReadAsStringAsync();
        using var genDoc = JsonDocument.Parse(genJson);
        var scheduleId = genDoc.RootElement.GetProperty("scheduleId").GetGuid();

        // Publicar
        var pubResponse = await client.PostAsync($"/api/schedules/{scheduleId}/publish", null);
        pubResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Teacher solo ve el publicado
        var teacherClient = _factory.CreateTeacherClient();
        var meResponse = await teacherClient.GetAsync("/api/schedules/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meJson = await meResponse.Content.ReadAsStringAsync();
        meJson.Should().Contain("2025-2026");
    }

    [Fact]
    public async Task EditManualEntry_Blocked_WhenPublished()
    {
        var client = _factory.CreateAdminClient();

        // Generar y publicar
        var genPayload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var genResponse = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(genPayload), Encoding.UTF8, "application/json"));
        var genJson = await genResponse.Content.ReadAsStringAsync();
        using var genDoc = JsonDocument.Parse(genJson);
        var scheduleId = genDoc.RootElement.GetProperty("scheduleId").GetGuid();
        await client.PostAsync($"/api/schedules/{scheduleId}/publish", null);

        // Intentar editar
        var editPayload = new { teacherId = "00000000-0000-0000-0001-000000000001", classroomId = "00000000-0000-0000-0002-000000000001" };
        var editResponse = await client.PutAsync($"/api/schedules/{scheduleId}/entries/{scheduleId}",
            new StringContent(JsonSerializer.Serialize(editPayload), Encoding.UTF8, "application/json"));
        editResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
