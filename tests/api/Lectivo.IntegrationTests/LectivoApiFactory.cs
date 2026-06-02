using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Lectivo.IntegrationTests;

public class LectivoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public LectivoApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
            });
        });
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Email", "elena.castro@ceip-miguel-hernandez.es");
        return client;
    }

    public HttpClient CreateTeacherClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Email", "laura.fernandez@ceip-miguel-hernandez.es");
        return client;
    }

    public HttpClient CreateClientForUserId(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }
}
