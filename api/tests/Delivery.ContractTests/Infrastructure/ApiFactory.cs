using System.Net.Http.Headers;
using Delivery.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Delivery.ContractTests.Infrastructure;

/// <summary>
/// L'API complète sur une base PostgreSQL neuve. `TEST_DATABASE_URL` (serveur
/// local déjà lancé) évite Docker sur le poste ; sinon Testcontainers.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "contract-tests-secret-contract-tests-secret-0123456789";

    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;
    private string? _databaseName;
    private string? _serverConnection;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(server))
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }
        else
        {
            _serverConnection = server;
            _databaseName = "wimo_test_" + Guid.NewGuid().ToString("N")[..12];
            await using (var connection = new NpgsqlConnection(server))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
                await command.ExecuteNonQueryAsync();
            }
            _connectionString = new NpgsqlConnectionStringBuilder(server) { Database = _databaseName }.ConnectionString;
        }

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _connectionString,
            ["JWT_SECRET"] = JwtSecret,
            ["UPLOADS_DIR"] = Path.Combine(Path.GetTempPath(), "wimo-tests-uploads"),
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }

    public HttpClient CreateMobileClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("x-client", "mobile");
        return client;
    }

    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, BaseAddress = new Uri("http://localhost") });

    public static void UseBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public async Task ExecuteDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> QueryDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else if (_serverConnection is not null && _databaseName is not null)
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_serverConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
