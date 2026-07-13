using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class AdminAuthorizationTests
{
    private readonly LectivoApiFactory _factory;

    public AdminAuthorizationTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Teacher_Cannot_Create_Classroom()
    {
        var client = _factory.CreateTeacherClient();
        var payload = JsonSerializer.Serialize(new
        {
            name = "Aula test",
            classroomType = "Classroom",
            capacity = 25,
            isShared = false,
            stageId = (Guid?)null
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/classrooms", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_Cannot_Create_Constraint()
    {
        var client = _factory.CreateTeacherClient();
        var payload = JsonSerializer.Serialize(new
        {
            teacherId = Guid.NewGuid(),
            constraintType = "Preferred",
            dayOfWeek = 1,
            slotIndex = 1,
            weight = 1,
            reason = (string?)null
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/constraints", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
