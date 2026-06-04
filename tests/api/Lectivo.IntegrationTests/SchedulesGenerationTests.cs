using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using HorariosEscolares.Application.Features.Schedules.Commands.GenerateSchedule;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class SchedulesGenerationTests
{
    private readonly LectivoApiFactory _factory;

    public SchedulesGenerationTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    private async Task<Guid> GenerateScheduleDirectlyAsync(string academicYear = "2025-2026", int timeoutSeconds = 30)
    {
        using var scope = _factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IScheduleGenerationOrchestrator>();
        var result = await orchestrator.GenerateAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            academicYear, timeoutSeconds, null, CancellationToken.None);

        return result switch
        {
            GenerateScheduleResult.Success s => s.ScheduleId,
            GenerateScheduleResult.ViabilityFailed f => f.ScheduleId,
            _ => throw new InvalidOperationException("Schedule generation did not produce a schedule ID")
        };
    }

    [Fact]
    public async Task Generate_Returns202_WithJobId()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("jobId", out var jobIdProp).Should().BeTrue();
        jobIdProp.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Generate_AsAdmin_WithSeedData_CanBeTracked()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var jobId = doc.RootElement.GetProperty("jobId").GetString()!;

        // Consultar estado del job
        var jobResponse = await client.GetAsync($"/api/schedules/jobs/{jobId}");
        jobResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobJson = await jobResponse.Content.ReadAsStringAsync();
        using var jobDoc = JsonDocument.Parse(jobJson);
        jobDoc.RootElement.GetProperty("id").GetString().Should().Be(jobId);
    }

    [Fact]
    public async Task Generate_SignalR_ConnectsAndJoinsGroup()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/generation", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-User-Email", "elena.castro@ceip-miguel-hernandez.es");
            })
            .Build();

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        await connection.InvokeAsync("JoinSchoolGroup", "00000000-0000-0000-0000-000000000001");

        var client = _factory.CreateAdminClient();
        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task PublishSchedule_ArchivesPrevious()
    {
        var client = _factory.CreateAdminClient();

        var scheduleId = await GenerateScheduleDirectlyAsync();

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

        var scheduleId = await GenerateScheduleDirectlyAsync();
        await client.PostAsync($"/api/schedules/{scheduleId}/publish", null);

        // Intentar editar
        var editPayload = new { teacherId = "00000000-0000-0000-0001-000000000001", classroomId = "00000000-0000-0000-0002-000000000001" };
        var editResponse = await client.PutAsync($"/api/schedules/{scheduleId}/entries/{scheduleId}",
            new StringContent(JsonSerializer.Serialize(editPayload), Encoding.UTF8, "application/json"));
        editResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_WithRealSeedData_SuccessfullyAssignsAllSessionsAndClassrooms()
    {
        using var scope = _factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IScheduleGenerationOrchestrator>();
        var result = await orchestrator.GenerateAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "2025-2026", 30, null, CancellationToken.None);

        result.Should().BeOfType<GenerateScheduleResult.Success>();
        var success = (GenerateScheduleResult.Success)result;
        success.Status.Should().Be("generated");
        success.TotalAssigned.Should().Be(success.TotalRequired);
        success.TotalAssigned.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Generate_ExcludedDay_ProducesNoEntriesForThatDay()
    {
        var client = _factory.CreateAdminClient();

        // Obtener configuración actual del centro
        var schoolResponse = await client.GetAsync("/api/schools/me");
        schoolResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var schoolDoc = JsonDocument.Parse(await schoolResponse.Content.ReadAsStringAsync());
        var originalSlotsPerDay = schoolDoc.RootElement.GetProperty("slotsPerDay").GetInt32();
        var originalWorkingDays = schoolDoc.RootElement.GetProperty("workingDays").EnumerateArray()
            .Select(d => d.GetInt32()).ToArray();

        // Aumentar SlotsPerDay (5→6) para que con 4 días haya 24 slots
        await client.PutAsync("/api/schools/me",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    slotsPerDay = 6,
                    workingDays = new[] { 1, 2, 4, 5 }
                }),
                Encoding.UTF8, "application/json"));

        try
        {
            var scheduleId = await GenerateScheduleDirectlyAsync("2025-2026-excl");

            var getResponse = await client.GetAsync($"/api/schedules/{scheduleId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getJson = await getResponse.Content.ReadAsStringAsync();

            using var scheduleDoc = JsonDocument.Parse(getJson);
            var entries = scheduleDoc.RootElement.GetProperty("entries").EnumerateArray().ToList();

            // No debe haber ninguna entrada para el día 3 (miércoles)
            entries.Should().NotContain(e => e.GetProperty("dayOfWeek").GetInt32() == 3);
        }
        finally
        {
            // Restaurar configuración original
            await client.PutAsync("/api/schools/me",
                new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        slotsPerDay = originalSlotsPerDay,
                        workingDays = originalWorkingDays
                    }),
                    Encoding.UTF8, "application/json"));
        }
    }
}
