using DotNet.Testcontainers.Builders;
using Testcontainers.MsSql;

namespace Lectivo.IntegrationTests;

/// <summary>
/// Fixture compartido que arranca un contenedor SQL Server efímero para toda la colección de tests.
/// </summary>
public class MsSqlFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public MsSqlFixture()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("MsSql collection")]
public class MsSqlCollection : ICollectionFixture<MsSqlFixture> { }
