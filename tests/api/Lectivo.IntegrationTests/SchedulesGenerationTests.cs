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
    private static readonly Guid SchoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid StageId = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private static readonly Guid PeriodId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly Guid InfantilStageId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid InfantilPeriodId = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly Guid SecundariaStageId = Guid.Parse("00000000-0000-0000-0000-000000000032");
    private static readonly Guid SecundariaPeriodId = Guid.Parse("00000000-0000-0000-0000-000000000023");

    public SchedulesGenerationTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    private async Task<Guid> GenerateScheduleDirectlyAsync(string academicYear = "2025-2026", int timeoutSeconds = 30)
    {
        using var scope = _factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IScheduleGenerationOrchestrator>();
        var result = await orchestrator.GenerateAsync(
            SchoolId, StageId, PeriodId, academicYear, Guid.Empty, timeoutSeconds, null, CancellationToken.None);

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

        var payload = new { stageId = StageId, periodId = PeriodId, academicYear = "2025-2026", timeoutSeconds = 30 };
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

        var payload = new { stageId = StageId, periodId = PeriodId, academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var jobId = doc.RootElement.GetProperty("jobId").GetString()!;

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
        var payload = new { stageId = StageId, periodId = PeriodId, academicYear = "2025-2026", timeoutSeconds = 30 };
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

        var pubResponse = await client.PostAsync($"/api/schedules/{scheduleId}/publish", null);
        pubResponse.StatusCode.Should().Be(HttpStatusCode.OK);

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
            SchoolId, StageId, PeriodId, "2025-2026", Guid.Empty, 30, null, CancellationToken.None);

        result.Should().BeOfType<GenerateScheduleResult.Success>();
        var success = (GenerateScheduleResult.Success)result;
        success.Status.Should().Be("generated");
    }

    [Fact]
    public async Task Generate_Infantil_WithSeedData_SuccessfullyAssignsAllSessions()
    {
        using var scope = _factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IScheduleGenerationOrchestrator>();
        var result = await orchestrator.GenerateAsync(
            SchoolId, InfantilStageId, InfantilPeriodId, "2025-2026", Guid.Empty, 30, null, CancellationToken.None);

        result.Should().BeOfType<GenerateScheduleResult.Success>();
        var success = (GenerateScheduleResult.Success)result;
        success.Status.Should().Be("generated");
    }

    [Fact]
    public async Task Generate_Secundaria_WithSeedData_SuccessfullyAssignsAllSessions()
    {
        using var scope = _factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IScheduleGenerationOrchestrator>();
        var result = await orchestrator.GenerateAsync(
            SchoolId, SecundariaStageId, SecundariaPeriodId, "2025-2026", Guid.Empty, 30, null, CancellationToken.None);

        result.Should().BeOfType<GenerateScheduleResult.Success>();
        var success = (GenerateScheduleResult.Success)result;
        success.Status.Should().Be("generated");
    }

    [Fact]
    public async Task Generate_Infantil_Bilingue_Partida_WithSeedData_SuccessfullyAssignsAllSessions()
    {
        var client = _factory.CreateAdminClient();
        try
        {
            var reseedPayload = new { modality = "bilingue", scheduleType = "partida" };
            var reseedResponse = await client.PostAsync("/api/dev/reseed",
                new StringContent(JsonSerializer.Serialize(reseedPayload), Encoding.UTF8, "application/json"));
            reseedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IScheduleGenerationOrchestrator>();
            var result = await orchestrator.GenerateAsync(
                SchoolId, InfantilStageId, InfantilPeriodId, "2025-2026", Guid.Empty, 30, null, CancellationToken.None);

            result.Should().BeOfType<GenerateScheduleResult.Success>();
            var success = (GenerateScheduleResult.Success)result;
            success.Status.Should().Be("generated");
        }
        finally
        {
            var restorePayload = new { modality = "estandar", scheduleType = "continua" };
            await client.PostAsync("/api/dev/reseed",
                new StringContent(JsonSerializer.Serialize(restorePayload), Encoding.UTF8, "application/json"));
        }
    }
}
