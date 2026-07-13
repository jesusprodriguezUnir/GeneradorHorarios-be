using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class TeachersCrudTests
{
    private readonly LectivoApiFactory _factory;

    public TeachersCrudTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Crud_FullCycle_AsAdmin()
    {
        var client = _factory.CreateAdminClient();

        // CREATE
        var createPayload = new
        {
            fullName = "Test Teacher",
            email = "test.teacher@ceip-miguel-hernandez.es",
            teacherType = "definitivo",
            maxWeeklyHours = 25,
            specialties = new[] { "Generalista" },
            colorKey = "mat",
        };
        var createResponse = await client.PostAsync("/api/teachers",
            new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createJson = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createJson);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        // GET by id
        var getResponse = await client.GetAsync($"/api/teachers/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // UPDATE
        var updatePayload = new { fullName = "Test Teacher Updated" };
        var updateResponse = await client.PutAsync($"/api/teachers/{id}",
            new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateJson = await updateResponse.Content.ReadAsStringAsync();
        updateJson.Should().Contain("Test Teacher Updated");

        // DELETE
        var deleteResponse = await client.DeleteAsync($"/api/teachers/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_AsTeacher_Returns403()
    {
        var client = _factory.CreateTeacherClient();

        var payload = new
        {
            fullName = "Hacker",
            email = "hacker@ceip-miguel-hernandez.es",
            teacherType = "definitivo",
            maxWeeklyHours = 25,
            specialties = new[] { "X" },
            colorKey = "mat",
        };
        var response = await client.PostAsync("/api/teachers",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_ReturnsSeedTeachers()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/teachers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().BeGreaterThan(5); // seed tiene 8 profesores
    }

    /// <summary>
    /// Verifica que un profesor puede tener varias StageAssignments con diferente ciclo
    /// (multi-ciclo dentro de la misma etapa) y que el endpoint PUT + GET hace el
    /// round-trip correcto.
    /// </summary>
    [Fact]
    public async Task Update_MultiCycleStageAssignments_RoundTripsCorrectly()
    {
        var client = _factory.CreateAdminClient();

        // ── 1. Obtener el stageId de Primaria del seed ───────────────────────
        var stagesResponse = await client.GetAsync("/api/schools/me/stages");
        stagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var stagesJson = await stagesResponse.Content.ReadAsStringAsync();
        using var stagesDoc = JsonDocument.Parse(stagesJson);
        var primariaStageId = stagesDoc.RootElement.EnumerateArray()
            .First(s => s.GetProperty("stageType").GetString() == "primaria")
            .GetProperty("id").GetGuid();

        // ── 2. Crear un profesor de prueba ────────────────────────────────────
        var createPayload = new
        {
            fullName      = "Profesor MultiCiclo Test",
            email         = "multiciclo.test@ceip-miguel-hernandez.es",
            teacherType   = "definitivo",
            maxWeeklyHours = 25,
            colorKey      = "mus",
            subjectHours  = Array.Empty<object>(),
            stageAssignments = Array.Empty<object>(),
        };
        var createResponse = await client.PostAsync("/api/teachers",
            new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createJson = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createJson);
        var teacherId = createDoc.RootElement.GetProperty("id").GetGuid();

        try
        {
            // ── 3. Actualizar con varias StageAssignments (ciclos 1 y 2 de Primaria) ─
            var updatePayload = new
            {
                stageAssignments = new[]
                {
                    new { stageId = primariaStageId, cycle = (int?)1 },
                    new { stageId = primariaStageId, cycle = (int?)2 },
                },
            };
            var updateResponse = await client.PutAsync($"/api/teachers/{teacherId}",
                new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // ── 4. GET y verificar el round-trip ─────────────────────────────
            var getResponse = await client.GetAsync($"/api/teachers/{teacherId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getJson = await getResponse.Content.ReadAsStringAsync();
            using var getDoc = JsonDocument.Parse(getJson);

            var assignments = getDoc.RootElement.GetProperty("stageAssignments").EnumerateArray().ToList();
            assignments.Should().HaveCount(2, "deben persistir exactamente los dos ciclos enviados");

            var cycles = assignments
                .Select(a => a.GetProperty("cycle").ValueKind == JsonValueKind.Null
                    ? (int?)null
                    : (int?)a.GetProperty("cycle").GetInt32())
                .OrderBy(c => c)
                .ToList();
            cycles.Should().Equal(new int?[] { 1, 2 }, "los ciclos devueltos deben ser 1 y 2");

            foreach (var a in assignments)
            {
                a.GetProperty("stageId").GetGuid().Should().Be(primariaStageId,
                    "ambas asignaciones deben pertenecer a la etapa de Primaria");
            }
        }
        finally
        {
            // ── 5. Limpiar ────────────────────────────────────────────────────
            await client.DeleteAsync($"/api/teachers/{teacherId}");
        }
    }
}
