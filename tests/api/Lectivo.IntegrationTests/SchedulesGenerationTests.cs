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

    [Fact]
    public async Task Generate_WithRealSeedData_SuccessfullyAssignsAllSessionsAndClassrooms()
    {
        var client = _factory.CreateAdminClient();

        var payload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
        var response = await client.PostAsync("/api/schedules/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        doc.RootElement.GetProperty("status").GetString().Should().Be("generated");
        
        var totalAssigned = doc.RootElement.GetProperty("totalAssigned").GetInt32();
        var totalRequired = doc.RootElement.GetProperty("totalRequired").GetInt32();
        
        totalAssigned.Should().Be(totalRequired);
        totalAssigned.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Generate_WithNonSpecialistTeacher_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        // 1. Crear un profesor sin la especialidad de Inglés
        var teacherPayload = new
        {
            fullName = "Profesor No Especialista",
            email = "no-especialista@ceip-miguel-hernandez.es",
            teacherType = "definitivo",
            maxWeeklyHours = 25,
            specialties = new[] { "Generalista" },
            colorKey = "mat"
        };
        var createTeacherResponse = await client.PostAsync("/api/teachers",
            new StringContent(JsonSerializer.Serialize(teacherPayload), Encoding.UTF8, "application/json"));
        createTeacherResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        using var teacherDoc = JsonDocument.Parse(await createTeacherResponse.Content.ReadAsStringAsync());
        var teacherId = teacherDoc.RootElement.GetProperty("id").GetGuid();

        // 2. Crear una asignación de Inglés para este profesor
        var assignmentPayload = new
        {
            teacherId = teacherId,
            groupId = Guid.Parse("00000000-0000-0000-0003-000000000001"), // 1ºA
            allocationId = Guid.Parse("00000000-0000-0000-0004-000000000004"), // Inglés (sIng)
            weeklyHours = 2
        };
        var createAssignmentResponse = await client.PostAsync("/api/assignments",
            new StringContent(JsonSerializer.Serialize(assignmentPayload), Encoding.UTF8, "application/json"));
        createAssignmentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        using var assignmentDoc = JsonDocument.Parse(await createAssignmentResponse.Content.ReadAsStringAsync());
        var assignmentId = assignmentDoc.RootElement.GetProperty("id").GetGuid();

        try
        {
            // 3. Lanzar la generación y comprobar que falla por especialidad
            var generatePayload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
            var generateResponse = await client.PostAsync("/api/schedules/generate",
                new StringContent(JsonSerializer.Serialize(generatePayload), Encoding.UTF8, "application/json"));

            generateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var generateJson = await generateResponse.Content.ReadAsStringAsync();
            generateJson.Should().Contain("no tiene la especialidad requerida para impartir");
        }
        finally
        {
            // Limpieza
            await client.DeleteAsync($"/api/assignments/{assignmentId}");
            await client.DeleteAsync($"/api/teachers/{teacherId}");
        }
    }

    [Fact]
    public async Task Generate_WithMissingClassroomType_ReturnsBadRequest()
    {
        var client = _factory.CreateAdminClient();

        // 1. Obtener las aulas actuales
        var getClassroomsResponse = await client.GetAsync("/api/classrooms");
        getClassroomsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using var classroomsDoc = JsonDocument.Parse(await getClassroomsResponse.Content.ReadAsStringAsync());
        var gymClassroom = classroomsDoc.RootElement.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("classroomType").GetString() == "gym");

        gymClassroom.Should().NotBeNull();
        var gymId = gymClassroom.GetProperty("id").GetGuid();
        var gymName = gymClassroom.GetProperty("name").GetString();
        var gymCapacity = gymClassroom.GetProperty("capacity").GetInt32();
        var gymIsShared = gymClassroom.GetProperty("isShared").GetBoolean();

        // 2. Eliminar temporalmente el Gimnasio
        var deleteResponse = await client.DeleteAsync($"/api/classrooms/{gymId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        try
        {
            // 3. Lanzar la generación y comprobar que falla por falta de aula gym (requerida por E. Física)
            var generatePayload = new { academicYear = "2025-2026", timeoutSeconds = 30 };
            var generateResponse = await client.PostAsync("/api/schedules/generate",
                new StringContent(JsonSerializer.Serialize(generatePayload), Encoding.UTF8, "application/json"));

            generateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var generateJson = await generateResponse.Content.ReadAsStringAsync();
            generateJson.Should().Contain("Falta configuración de espacio");
        }
        finally
        {
            // 4. Restaurar el Gimnasio
            var restorePayload = new
            {
                name = gymName,
                classroomType = "gym",
                capacity = gymCapacity,
                isShared = gymIsShared
            };
            await client.PostAsync("/api/classrooms",
                new StringContent(JsonSerializer.Serialize(restorePayload), Encoding.UTF8, "application/json"));
        }
    }
}
