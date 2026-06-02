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
}
