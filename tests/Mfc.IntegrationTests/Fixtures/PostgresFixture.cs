using System.Globalization;
using DotNet.Testcontainers.Containers;
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

    /// <summary>
    /// Runs <c>pg_dump -Fc</c> then <c>pg_restore</c> inside the Postgres container (M6-08 AC11).
    /// Avoids host client version skew and host volume mount issues.
    /// </summary>
    public async Task DumpAndRestoreAsync(
        string sourceConnectionString,
        string targetConnectionString,
        CancellationToken cancellationToken = default)
    {
        string sourceDb = new NpgsqlConnectionStringBuilder(sourceConnectionString).Database
            ?? throw new InvalidOperationException("Source connection string has no database.");
        string targetDb = new NpgsqlConnectionStringBuilder(targetConnectionString).Database
            ?? throw new InvalidOperationException("Target connection string has no database.");
        string dumpPath = "/tmp/mfc-m608-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".dump";

        ExecResult dump = await _container.ExecAsync(
            ["pg_dump", "-U", "mfc", "-d", sourceDb, "-Fc", "-f", dumpPath],
            cancellationToken).ConfigureAwait(false);
        if (dump.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"pg_dump failed ({dump.ExitCode}): {dump.Stderr}{dump.Stdout}");
        }

        ExecResult restore = await _container.ExecAsync(
            ["pg_restore", "-U", "mfc", "-d", targetDb, "--no-owner", "--no-acl", dumpPath],
            cancellationToken).ConfigureAwait(false);
        if (restore.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"pg_restore failed ({restore.ExitCode}): {restore.Stderr}{restore.Stdout}");
        }

        _ = await _container.ExecAsync(["rm", "-f", dumpPath], cancellationToken).ConfigureAwait(false);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresSharedFixtureDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
