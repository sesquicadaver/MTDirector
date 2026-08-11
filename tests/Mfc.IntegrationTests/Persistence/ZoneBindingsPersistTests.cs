using Mfc.Application.Abstractions.Persistence;
using Mfc.Controller;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Persistence;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class ZoneBindingsPersistTests
{
    private readonly PostgresFixture _postgres;

    public ZoneBindingsPersistTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateCreatesZoneTablesAndSchemaMetadata()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("ZoneBindingsSchemaM205", StringComparison.Ordinal));

        Assert.NotNull(await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.ZoneBindingsSchemaKey));

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY['zone_definitions', 'node_zone_bindings'])
            ORDER BY table_name;
            """;
        List<string> tables = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(["node_zone_bindings", "zone_definitions"], tables);
    }

    [Fact]
    public async Task PersistRoundTripAndUniqueNodeZoneConstraint()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ISiteStore sites = scope.ServiceProvider.GetRequiredService<ISiteStore>();
        INodeStore nodes = scope.ServiceProvider.GetRequiredService<INodeStore>();
        IZoneDefinitionStore zones = scope.ServiceProvider.GetRequiredService<IZoneDefinitionStore>();
        INodeZoneBindingStore bindings = scope.ServiceProvider.GetRequiredService<INodeZoneBindingStore>();

        Site site = Site.Create(SiteCode.Create("ZLAB"), NonEmptyName.Create("Zone Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("core"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);

        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company,
            null,
            NonEmptyName.Create("lan"),
            NonEmptyName.Create("LAN"));
        await zones.AddAsync(zone);

        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            []);
        NodeZoneBinding binding = NodeZoneBinding.Create(
            node.Id,
            zone.Id,
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            expected);
        await bindings.AddAsync(binding);

        NodeZoneBinding? loaded = await bindings.GetAsync(binding.Id);
        Assert.NotNull(loaded);
        Assert.Equal(expected.ToString(), loaded.ExpectedDependencyHash.ToString());
        Assert.True(loaded.AnalysisStale);

        NodeZoneBinding duplicate = NodeZoneBinding.Create(
            node.Id,
            zone.Id,
            NodeZoneBindingKind.SingleInterface,
            ["ether2"],
            expected);
        await Assert.ThrowsAsync<DbUpdateException>(() => bindings.AddAsync(duplicate));
    }

    [Fact]
    public async Task UpdatePersistsRecordResolveAndRowVersion()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ISiteStore sites = scope.ServiceProvider.GetRequiredService<ISiteStore>();
        INodeStore nodes = scope.ServiceProvider.GetRequiredService<INodeStore>();
        IZoneDefinitionStore zones = scope.ServiceProvider.GetRequiredService<IZoneDefinitionStore>();
        INodeZoneBindingStore bindings = scope.ServiceProvider.GetRequiredService<INodeZoneBindingStore>();

        Site site = Site.Create(SiteCode.Create("ZLAB2"), NonEmptyName.Create("Zone Lab 2"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("core"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company,
            null,
            NonEmptyName.Create("dmz"),
            NonEmptyName.Create("DMZ"));
        await zones.AddAsync(zone);

        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            ["ether3"],
            ["ether3"]);
        NodeZoneBinding binding = NodeZoneBinding.Create(
            node.Id,
            zone.Id,
            NodeZoneBindingKind.SingleInterface,
            ["ether3"],
            expected);
        await bindings.AddAsync(binding);

        Hash256 fresh = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            ["ether3"],
            ["ether3"]);
        binding.RecordResolve(fresh);
        await bindings.UpdateAsync(binding);

        NodeZoneBinding? loaded = await bindings.GetAsync(binding.Id);
        Assert.NotNull(loaded);
        Assert.Equal(2UL, loaded.RowVersion);
        Assert.False(loaded.AnalysisStale);
        Assert.Equal(fresh.ToString(), loaded.LastResolvedDependencyHash!.ToString());
    }

    private static WebApplication BuildApp(string connectionString)
    {
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        return Program.BuildHost(
            [
                "--environment", "Development",
                $"--Mfc:Grpc:ListenAddress={url}",
                "--Mfc:Grpc:AllowInsecureLoopback=true",
                "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
                "--Mfc:Security:RequireTls=true",
                "--Mfc:Security:MasterKeyProvider=Development",
                "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                $"--Mfc:Database:ConnectionString={connectionString}",
            ]);
    }

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
