using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using DomainAnchorKey = Mfc.Domain.Onboarding.AnchorKey;
using DomainHash = Mfc.Domain.Inventory.Primitives.Hash256;
using DomainIpFamily = Mfc.Domain.Inventory.IpAddressFamily;
using DomainNodeKind = Mfc.Domain.Inventory.NodeKind;
using ProtoDevice = Mfc.Contracts.Mfc.V1.Device;
using ProtoNode = Mfc.Contracts.Mfc.V1.Node;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoSite = Mfc.Contracts.Mfc.V1.Site;

namespace Mfc.IntegrationTests.Controller;

/// <summary>CT-DEPLOY-01: DeploymentService GrpcHost contract (CreatePlan/Start/Watch/Rollback/GetRecoveryStatus).</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class DeploymentGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public DeploymentGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task CreatePlanStartWatchAndRecoveryStatus()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<IDeploymentRuntime>();
                builder.Services.AddSingleton<IDeploymentRuntime>(new ScriptedDeploymentRuntime { Commit = true });
            });
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            DeploymentService.DeploymentServiceClient deployment = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            DeploymentPlanSummary plan = await deployment.CreatePlanAsync(
                PlanRequest(nodeId, deviceId),
                headers,
                deadline: Deadline());
            Assert.Equal(32, plan.PlanHash.Value.Length);

            RpcException mismatch = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await deployment.StartAsync(
                    new StartDeploymentRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        PlanId = plan.PlanId,
                        PlanHash = Utf8Sha256("wrong"),
                        PacketPathPairs = { CpuPair() },
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Contains("plan_hash", mismatch.Status.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(StatusCode.FailedPrecondition, mismatch.StatusCode);
            ErrorDetail? detail = TryReadErrorDetail(mismatch);
            Assert.NotNull(detail);
            Assert.Equal(DeploymentCodes.PlanHashMismatch, detail.Code);

            DeploymentOperationSummary started = await deployment.StartAsync(
                new StartDeploymentRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    PlanId = plan.PlanId,
                    PlanHash = plan.PlanHash,
                    PacketPathPairs = { CpuPair() },
                },
                headers,
                deadline: Deadline());
            Assert.Equal(global::Mfc.Contracts.Mfc.V1.DeploymentOperationState.Committed, started.State);

            List<DeploymentProgress> progress = [];
            using AsyncServerStreamingCall<DeploymentProgress> watch = deployment.Watch(
                new WatchDeploymentRequest { OperationId = started.OperationId },
                headers,
                deadline: Deadline());
            await foreach (DeploymentProgress item in watch.ResponseStream.ReadAllAsync())
            {
                progress.Add(item);
            }

            Assert.NotEmpty(progress);
            Assert.Equal(global::Mfc.Contracts.Mfc.V1.DeploymentOperationState.Committed, progress[^1].State);

            DeploymentRecoveryStatus status = await deployment.GetRecoveryStatusAsync(
                new GetDeploymentRecoveryStatusRequest
                {
                    NodeId = nodeId,
                    OperationId = started.OperationId,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(global::Mfc.Contracts.Mfc.V1.DeploymentRecoveryAction.KeepCommitted, status.Action);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task CreatePlanAndRollbackAreIdempotent()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<IDeploymentRuntime>();
                builder.Services.AddSingleton<IDeploymentRuntime>(new ScriptedDeploymentRuntime { Commit = false });
            });
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            DeploymentService.DeploymentServiceClient deployment = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            Guid planKey = Guid.NewGuid();
            CreateDeploymentPlanRequest planRequest = PlanRequest(nodeId, deviceId, planKey);
            DeploymentPlanSummary first = await deployment.CreatePlanAsync(planRequest, headers, deadline: Deadline());
            DeploymentPlanSummary replayed = await deployment.CreatePlanAsync(planRequest, headers, deadline: Deadline());
            Assert.Equal(first.PlanId, replayed.PlanId);

            DeploymentOperationSummary started = await deployment.StartAsync(
                new StartDeploymentRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    PlanId = first.PlanId,
                    PlanHash = first.PlanHash,
                    PacketPathPairs = { CpuPair() },
                },
                headers,
                deadline: Deadline());
            Assert.Equal(global::Mfc.Contracts.Mfc.V1.DeploymentOperationState.RollbackPending, started.State);

            Guid rollbackKey = Guid.NewGuid();
            RollbackDeploymentRequest rollback = new()
            {
                IdempotencyKey = ProtoUuid.FromGuid(rollbackKey),
                OperationId = started.OperationId,
            };
            DeploymentOperationSummary rolled = await deployment.RollbackAsync(rollback, headers, deadline: Deadline());
            DeploymentOperationSummary rolledAgain = await deployment.RollbackAsync(rollback, headers, deadline: Deadline());
            Assert.Equal(global::Mfc.Contracts.Mfc.V1.DeploymentOperationState.RolledBack, rolled.State);
            Assert.Equal(rolled.OperationId, rolledAgain.OperationId);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    private static async Task<(Uuid NodeId, Uuid DeviceId)> SeedRouterAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers)
    {
        ProtoSite site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "DP1",
                Name = "Deploy",
            },
            headers,
            deadline: Deadline());
        ProtoNode node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "edge",
                DeclaredKind = ProtoNodeKind.Router,
                DeclaredUplinkMode = global::Mfc.Contracts.Mfc.V1.DeclaredUplinkMode.One,
            },
            headers,
            deadline: Deadline());
        ProtoDevice device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                DisplayName = "edge",
                ManagementHost = "192.0.2.50",
                ManagementPort = 8729,
                Role = global::Mfc.Contracts.Mfc.V1.DeviceRole.Router,
            },
            headers,
            deadline: Deadline());
        return (node.Id, device.Id);
    }

    private static CreateDeploymentPlanRequest PlanRequest(Uuid nodeId, Uuid deviceId, Guid? idempotencyKey = null)
    {
        Sha256 hash = Utf8Sha256("deploy");
        return new CreateDeploymentPlanRequest
        {
            IdempotencyKey = ProtoUuid.FromGuid(idempotencyKey ?? Guid.NewGuid()),
            NodeId = nodeId,
            LogicalPolicyHash = hash,
            AnalysisBundleHash = hash,
            TopologyProjectionHash = hash,
            Devices = { BuildDevicePlan(deviceId) },
        };
    }

    private static DeploymentDevicePlanInput BuildDevicePlan(Uuid deviceId)
    {
        IReadOnlyList<DomainAnchorKey> keys = RequiredAnchorSet.For(DomainNodeKind.Router, includeIpv6: false);
        IReadOnlyList<DomainAnchorKey> activation = DeploymentAnchorOrder.Sort(keys);
        List<AnchorTarget> oldDomain = [];
        List<AnchorTarget> newDomain = [];
        List<DeploymentAnchorTargetInput> oldTargets = [];
        List<DeploymentAnchorTargetInput> newTargets = [];
        foreach (DomainAnchorKey key in activation)
        {
            string oldJump = BootstrapArtifact.RootChainName(key.Family, key.Chain);
            string newJump =
                $"mfc{(key.Family == DomainIpFamily.IPv4 ? "4" : "6")}.{DomainAnchorKey.ChainCode(key.Chain)}.r.0123456789abcdef";
            oldDomain.Add(new AnchorTarget(key, oldJump));
            newDomain.Add(new AnchorTarget(key, newJump));
            oldTargets.Add(new DeploymentAnchorTargetInput { Marker = key.Marker, JumpTarget = oldJump });
            newTargets.Add(new DeploymentAnchorTargetInput { Marker = key.Marker, JumpTarget = newJump });
        }

        TransitionStateValidationResult transitions = TransitionStateValidator.Validate(
            activation,
            oldDomain,
            newDomain,
            TransitionStateValidator.AllSafeEvidence(activation.Count));
        if (transitions.HasBlockers)
        {
            throw new InvalidOperationException(string.Join(';', transitions.Findings.Select(static f => f.Message)));
        }

        DeploymentDevicePlanInput input = new()
        {
            DeviceId = deviceId,
            ExpectedRouterosVersion = "7.16.2",
            ExpectedCapabilityHash = Utf8Sha256("cap"),
            ExpectedConfigurationHash = Utf8Sha256("cfg"),
            ExpectedCompatibilityHash = Utf8Sha256("compat"),
            ExpectedGuardContextHash = Utf8Sha256("guard"),
            ExpectedAnchorContextHash = Utf8Sha256("anchor"),
            OldArtifactHash = Utf8Sha256("old-art"),
            NewArtifactHash = Utf8Sha256("new-art"),
            RollbackTtlSeconds = 180,
        };
        input.OldAnchorTargets.AddRange(oldTargets);
        input.NewAnchorTargets.AddRange(newTargets);
        input.AnchorActivationOrderMarkers.AddRange(activation.Select(static k => k.Marker));
        foreach (DomainHash transition in transitions.TransitionStateHashes)
        {
            input.TransitionStateHashes.Add(new Sha256 { Value = ByteString.CopyFrom(transition.Bytes.ToArray()) });
        }

        input.Probes.Add(new DeploymentProbeInput
        {
            Kind = global::Mfc.Contracts.Mfc.V1.DeploymentProbeKind.RouterPing,
            Destination = "192.0.2.1",
            TimeoutMilliseconds = 500,
        });
        return input;
    }

    private static DeploymentPacketPathPairFact CpuPair()
        => new()
        {
            IngressInterface = "ether1",
            EgressInterface = "wan1",
            PathClass = DeploymentPacketPathKind.CpuFirewall,
        };

    private static Sha256 Utf8Sha256(string value)
        => new() { Value = ByteString.CopyFrom(SHA256.HashData(Encoding.UTF8.GetBytes(value))) };

    private static string[] DevArgs(string url, string connectionString)
        =>
        [
            "--environment", "Development",
            $"--Mfc:Grpc:ListenAddress={url}",
            "--Mfc:Grpc:AllowInsecureLoopback=true",
            "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
            "--Mfc:Security:RequireTls=true",
            "--Mfc:Security:MasterKeyProvider=Development",
            "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
            "--Mfc:OperationalJobs:Enabled=false",
            $"--Mfc:Database:ConnectionString={connectionString}",
        ];

    private static Metadata ActorHeaders(string actor) => new()
    {
        { InventoryGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(60);

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(string url, TimeSpan timeout)
    {
        Uri uri = new(url);
        using CancellationTokenSource delay = new(timeout);
        while (!delay.IsCancellationRequested)
        {
            try
            {
                using System.Net.Sockets.TcpClient client = new();
                await client.ConnectAsync(uri.Host, uri.Port, delay.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(50, delay.Token);
            }
        }

        throw new TimeoutException($"Timed out waiting for {url}");
    }

    private static ErrorDetail? TryReadErrorDetail(RpcException exception)
    {
        byte[]? bytes = exception.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
        return bytes is null ? null : ErrorDetail.Parser.ParseFrom(bytes);
    }
}
