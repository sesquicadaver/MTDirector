using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Controller;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
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
public sealed class DeploymentPersistTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 19, 21, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public DeploymentPersistTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateCreatesDeploymentTablesAndSchemaMetadata()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("DeploymentSchemaM401", StringComparison.Ordinal));
        Assert.NotNull(await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.DeploymentSchemaKey));
        Assert.Equal(
            SchemaMetadataEntitySeed.DeploymentSchemaValue,
            (await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.DeploymentSchemaKey))!.Value);

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY[
                'deployment_plans',
                'deployment_device_plans',
                'deployment_operations',
                'deployment_device_states',
                'deployment_locks',
                'deployment_steps'])
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
                "deployment_device_plans",
                "deployment_device_states",
                "deployment_locks",
                "deployment_operations",
                "deployment_plans",
                "deployment_steps",
            ],
            tables);

        cmd.CommandText = """
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'deployment_operations'
              AND indexname = 'uq_deployment_operations_node_nonterminal';
            """;
        object? index = await cmd.ExecuteScalarAsync();
        Assert.Equal("uq_deployment_operations_node_nonterminal", index);

        cmd.CommandText = """
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'deployment_locks'
              AND indexname IN ('uq_deployment_locks_node', 'PK_deployment_locks');
            """;
        object? lockIndex = await cmd.ExecuteScalarAsync();
        Assert.NotNull(lockIndex);
    }

    [Fact]
    public async Task PlanOperationLockStepRoundTripAndNonterminalUniqueConflict()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ISiteStore sites = scope.ServiceProvider.GetRequiredService<ISiteStore>();
        INodeStore nodes = scope.ServiceProvider.GetRequiredService<INodeStore>();
        IDeviceStore devices = scope.ServiceProvider.GetRequiredService<IDeviceStore>();
        IDeploymentStore deployments = scope.ServiceProvider.GetRequiredService<IDeploymentStore>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        (Node node, Device device, DeploymentPlan plan) = await SeedRouterAsync(sites, nodes, devices);
        await deployments.AddPlanAsync(plan);
        DeploymentPlan? loadedPlan = await deployments.GetPlanAsync(plan.Id);
        Assert.NotNull(loadedPlan);
        Assert.Equal(plan.PlanHash.ToString(), loadedPlan.PlanHash.ToString());
        Assert.Equal(device.Id, Assert.Single(loadedPlan.DevicePlans).DeviceId);

        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        await deployments.AddOperationAsync(operation);
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        await deployments.SaveOperationAsync(operation);
        DeploymentOperation? loadedOp = await deployments.GetOperationAsync(operation.Id);
        Assert.Equal(DeploymentOperationState.Prechecking, loadedOp!.State);
        Assert.Single(await deployments.ListNonterminalByNodeAsync(node.Id));

        DeviceDeployment member = DeviceDeployment.Create(operation.Id, device.Id, T0);
        await deployments.AddDeviceStateAsync(member);
        member.EnsureTransition(DeviceDeploymentState.Prechecked, T0.AddSeconds(1));
        await deployments.SaveDeviceStateAsync(member);
        Assert.Equal(DeviceDeploymentState.Prechecked, Assert.Single(await deployments.ListDeviceStatesAsync(operation.Id)).State);

        DeploymentLock held = DeploymentLock.Acquire(node.Id, operation.Id, "instance-a", T0);
        await deployments.AddLockAsync(held);
        held.Heartbeat("instance-a", T0.AddMinutes(1));
        await deployments.SaveLockAsync(held);
        DeploymentLock? loadedLock = await deployments.GetLockByNodeAsync(node.Id);
        Assert.NotNull(loadedLock);
        Assert.Equal("instance-a", loadedLock.OwnerInstanceId);
        Assert.True(loadedLock.ExpiresAtUtc > T0.AddMinutes(1));

        DeploymentStep step = DeploymentStep.Create(
            operation.Id,
            device.Id,
            1,
            DeploymentStepKind.StageFilterChain,
            H("before"),
            H("after"),
            T0);
        await deployments.AddStepAsync(step);
        step.RecordEffectSent(T0.AddSeconds(2));
        await deployments.SaveStepAsync(step);
        DeploymentStep loadedStep = Assert.Single(await deployments.ListStepsAsync(operation.Id));
        Assert.Equal(DeploymentStepState.EffectSent, loadedStep.State);

        DeploymentOperation second = DeploymentOperation.Create(plan, UserId.New(), T0.AddMinutes(1));
        PersistenceConflictException conflict = await Assert.ThrowsAsync<PersistenceConflictException>(
            () => deployments.AddOperationAsync(second));
        Assert.Equal(DeploymentCodes.NonterminalExists, conflict.Code);

        DeploymentLock other = DeploymentLock.Reconstitute(
            node.Id,
            operation.Id,
            "instance-b",
            T0,
            T0,
            T0.AddMinutes(2));
        PersistenceConflictException lockConflict = await Assert.ThrowsAsync<PersistenceConflictException>(
            () => deployments.AddLockAsync(other));
        Assert.Equal(DeploymentCodes.LockHeld, lockConflict.Code);

        db.ChangeTracker.Clear();
        DeploymentPlanEntity planEntity = await db.DeploymentPlans.SingleAsync(p => p.Id == plan.Id.Value);
        planEntity.ExpiresAtUtc = planEntity.ExpiresAtUtc.AddMinutes(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        step.MarkVerified(T0.AddSeconds(3));
        await deployments.SaveStepAsync(step);
        db.ChangeTracker.Clear();
        DeploymentStepEntity stepEntity = await db.DeploymentSteps.SingleAsync(s => s.Id == step.Id.Value);
        stepEntity.State = DeploymentStepEntity.FailedState;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        loadedOp.EnsureTransition(DeploymentOperationState.NoChanges, T0.AddSeconds(4));
        await deployments.SaveOperationAsync(loadedOp);
        db.ChangeTracker.Clear();
        DeploymentOperationEntity opEntity = await db.DeploymentOperations.SingleAsync(o => o.Id == operation.Id.Value);
        opEntity.State = DeploymentOperationEntity.CommittedState;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        Assert.Empty(await deployments.ListNonterminalByNodeAsync(node.Id));
        DeploymentOperation afterTerminal = DeploymentOperation.Create(plan, UserId.New(), T0.AddMinutes(2));
        await deployments.AddOperationAsync(afterTerminal);
        Assert.Single(await deployments.ListNonterminalByNodeAsync(node.Id));
    }

    private static async Task<(Node Node, Device Device, DeploymentPlan Plan)> SeedRouterAsync(
        ISiteStore sites,
        INodeStore nodes,
        IDeviceStore devices)
    {
        Site site = Site.Create(SiteCode.Create("DP1"), NonEmptyName.Create("Deploy Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("edge"), NodeKind.Router, DeclaredUplinkMode.One);
        Device device = node.AddDevice(
            NonEmptyName.Create("edge-dev"),
            ManagementEndpoint.Create("10.60.0.1"),
            DeviceRole.Router);
        await nodes.AddAsync(node);
        await devices.AddAsync(device);
        return (node, device, PlanFor(node, device));
    }

    private static DeploymentPlan PlanFor(Node node, Device device)
    {
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(NodeKind.Router, includeIpv6: false);
        List<AnchorTarget> oldTargets = [];
        List<AnchorTarget> newTargets = [];
        foreach (AnchorKey key in keys)
        {
            oldTargets.Add(new AnchorTarget(key, BootstrapArtifact.RootChainName(key.Family, key.Chain)));
            newTargets.Add(new AnchorTarget(
                key,
                $"mfc{(key.Family == IpAddressFamily.IPv4 ? "4" : "6")}.{AnchorKey.ChainCode(key.Chain)}.r.0123456789abcdef"));
        }

        DeviceDeploymentPlan devicePlan = DeviceDeploymentPlan.Create(
            device.Id,
            "7.16.2",
            H("cap"),
            H("cfg"),
            H("compat"),
            H("guard-ctx"),
            H("anchor-ctx"),
            H("old-art"),
            oldTargets,
            H("new-art"),
            newTargets,
            keys,
            keys.Reverse().ToArray(),
            [H("t0"), H("t1")],
            DeploymentCodes.DefaultRollbackTtl,
            [new DeploymentProbe(DeploymentProbeKind.IcmpEcho, "192.0.2.1", 500)]);
        return DeploymentPlan.Create(
            node,
            H("policy"),
            H("analysis"),
            H("topology"),
            [devicePlan],
            UserId.New(),
            T0);
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
