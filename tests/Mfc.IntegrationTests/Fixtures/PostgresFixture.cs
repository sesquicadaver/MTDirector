using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Mfc.IntegrationTests.Fixtures;

/// <summary>
/// Shared real PostgreSQL 18 container for persistence integration tests.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("mfc")
        .WithUsername("mfc")
        .WithPassword("mfc_test_only")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    /// <summary>
    /// Creates an isolated empty database on the shared container for a single test.
    /// </summary>
    public async Task<string> CreateFreshDatabaseAsync(CancellationToken cancellationToken = default)
    {
        string dbName = "mfc_" + Guid.NewGuid().ToString("N");
        NpgsqlConnectionStringBuilder admin = new(ConnectionString)
        {
            Database = "postgres",
        };

        await using NpgsqlConnection connection = new(admin.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        NpgsqlConnectionStringBuilder test = new(ConnectionString)
        {
            Database = dbName,
        };
        return test.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresSharedFixtureDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
