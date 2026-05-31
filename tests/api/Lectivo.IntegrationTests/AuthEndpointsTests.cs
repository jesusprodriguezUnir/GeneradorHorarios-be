using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Lectivo.IntegrationTests;

[Collection("MsSql collection")]
public class AuthEndpointsTests
{
    private readonly LectivoApiFactory _factory;

    public AuthEndpointsTests(MsSqlFixture fixture)
    {
        _factory = new LectivoApiFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetMe_WithValidHeader_Returns200AndRole()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("school_admin");
    }

    [Fact]
    public async Task GetMe_WithoutHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDemoUsers_ReturnsTwoUsers()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/demo-users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(2);
    }
}
