using Npgsql;
using Testcontainers.PostgreSql;

namespace ECommerce.Catalog.Api.Tests;

/// <summary>
/// Starts one disposable PostgreSQL server per test class (Docker must be running).
/// Each test then gets its own database, created by the migrations in Program.cs.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    /// <summary>Returns a connection string to a new, empty database on the server.</summary>
    public string NewDatabase() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"catalog_{Guid.NewGuid():N}"
        }.ConnectionString;

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
