using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Controller;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Persistence;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class OnboardingPersistTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public OnboardingPersistTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateCreatesOnboardingTablesAndSchemaMetadata()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("OnboardingSchemaM501", StringComparison.Ordinal));
        Assert.NotNull(await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.OnboardingSchemaKey));
        Assert.Equal(SchemaMetadataEntitySeed.OnboardingSchemaValue, (await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.OnboardingSchemaKey))!.Value);

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY[
                'onboarding_plans',
                'onboarding_device_plans',
                'onboarding_anchor_placements',
                'onboarding_operations',
                'onboarding_steps'])
            ORDER BY table_name;
            """;
        List<string> tables = [];
        {
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            [
                "onboarding_anchor_placements",
                "onboarding_device_plans",
                "onboarding_operations",
                "onboarding_plans",
                "onboarding_steps",
            ],
            tables);

        cmd.CommandText = """
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'onboarding_operations'
              AND indexname = 'uq_onboarding_operations_node_nonterminal';
            """;
        object? index = await cmd.ExecuteScalarAsync();
        Assert.Equal("uq_onboarding_operations_node_nonterminal", index);

        cmd.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'nodes' AND column_name = 'ManagementState';
            """;
        Assert.Equal("ManagementState", await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PlanOperationStepRoundTripAndNonterminalUniqueConflict()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ISiteStore sites = scope.ServiceProvider.GetRequiredService<ISiteStore>();
        INodeStore nodes = scope.ServiceProvider.GetRequiredService<INodeStore>();
        IDeviceStore devices = scope.ServiceProvider.GetRequiredService<IDeviceStore>();
        IOnboardingStore onboarding = scope.ServiceProvider.GetRequiredService<IOnboardingStore>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        (Node node, Device device, OnboardingPlan plan) = await SeedRouterAsync(sites, nodes, devices);
        await onboarding.AddPlanAsync(plan);
        OnboardingPlan? loadedPlan = await onboarding.GetPlanAsync(plan.Id);
        Assert.NotNull(loadedPlan);
        Assert.Equal(plan.PlanHash.ToString(), loadedPlan.PlanHash.ToString());
        Assert.Equal(device.Id, Assert.Single(loadedPlan.DevicePlans).DeviceId);

        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        await onboarding.AddOperationAsync(operation);
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        await onboarding.SaveOperationAsync(operation);
        OnboardingOperation? loadedOp = await onboarding.GetOperationAsync(operation.Id);
        Assert.Equal(OnboardingOperationState.Prechecking, loadedOp!.State);
        Assert.Single(await onboarding.ListNonterminalByNodeAsync(node.Id));

        OnboardingStep step = OnboardingStep.Create(
            operation.Id,
            device.Id,
            1,
            OnboardingStepKind.CreateBootstrapRoot,
            H("before"),
            H("after"),
            T0);
        await onboarding.AddStepAsync(step);
        step.RecordEffectSent(T0.AddSeconds(2));
        await onboarding.SaveStepAsync(step);
        OnboardingStep loadedStep = Assert.Single(await onboarding.ListStepsAsync(operation.Id));
        Assert.Equal(OnboardingStepState.EffectSent, loadedStep.State);

        OnboardingOperation second = OnboardingOperation.Create(plan, UserId.New(), T0.AddMinutes(1));
        PersistenceConflictException conflict = await Assert.ThrowsAsync<PersistenceConflictException>(
            () => onboarding.AddOperationAsync(second));
        Assert.Equal(OnboardingCodes.NonterminalExists, conflict.Code);

        db.ChangeTracker.Clear();
        OnboardingPlanEntity planEntity = await db.OnboardingPlans.SingleAsync(p => p.Id == plan.Id.Value);
        planEntity.ExpiresAtUtc = planEntity.ExpiresAtUtc.AddMinutes(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        step.MarkVerified(T0.AddSeconds(3));
        await onboarding.SaveStepAsync(step);
        db.ChangeTracker.Clear();
        OnboardingStepEntity stepEntity = await db.OnboardingSteps.SingleAsync(s => s.Id == step.Id.Value);
        stepEntity.State = OnboardingStepEntity.FailedState;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        loadedOp.EnsureTransition(OnboardingOperationState.Blocked, T0.AddSeconds(4), OnboardingCodes.NamespaceCollision);
        await onboarding.SaveOperationAsync(loadedOp);
        db.ChangeTracker.Clear();
        OnboardingOperationEntity opEntity = await db.OnboardingOperations.SingleAsync(o => o.Id == operation.Id.Value);
        opEntity.State = OnboardingOperationEntity.RolledBackState;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        Assert.Empty(await onboarding.ListNonterminalByNodeAsync(node.Id));
        OnboardingOperation afterTerminal = OnboardingOperation.Create(plan, UserId.New(), T0.AddMinutes(2));
        await onboarding.AddOperationAsync(afterTerminal);
        Assert.Single(await onboarding.ListNonterminalByNodeAsync(node.Id));
    }

    private static async Task<(Node Node, Device Device, OnboardingPlan Plan)> SeedRouterAsync(
        ISiteStore sites,
        INodeStore nodes,
        IDeviceStore devices)
    {
        Site site = Site.Create(SiteCode.Create("OB1"), NonEmptyName.Create("Onboard Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("edge"), NodeKind.Router, DeclaredUplinkMode.One);
        Device device = node.AddDevice(
            NonEmptyName.Create("edge-dev"),
            ManagementEndpoint.Create("10.50.0.1"),
            DeviceRole.Router);
        await nodes.AddAsync(node);
        await devices.AddAsync(device);

        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(NodeKind.Router, includeIpv6: false);
        List<AnchorPlacement> placements = [];
        uint ordinal = 0;
        foreach (AnchorKey key in keys)
        {
            placements.Add(AnchorPlacement.Create(key.Family, key.Chain, AnchorPlacementMode.Append, ordinal));
            ordinal++;
        }

        DeviceOnboardingPlan devicePlan = DeviceOnboardingPlan.Create(
            device.Id,
            "7.16.2",
            H("cap"),
            H("cfg"),
            H("compat"),
            H("api"),
            H("read"),
            H("deploy"),
            H("mode"),
            H("guard"),
            keys,
            placements);
        OnboardingPlan plan = OnboardingPlan.Create(
            node,
            H("membership"),
            H("topology"),
            [devicePlan],
            UserId.New(),
            T0);
        return (node, device, plan);
    }

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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
