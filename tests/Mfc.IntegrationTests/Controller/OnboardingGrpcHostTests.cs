using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Mfc.IntegrationTests.Controller;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class OnboardingGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public OnboardingGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task ValidateCreateStartWatchAndRecoveryStatus()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<IOnboardingRuntime>();
                builder.Services.AddSingleton<IOnboardingRuntime>(new ScriptedOnboardingRuntime { Commit = true });
            });
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            OnboardingService.OnboardingServiceClient onboarding = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            OnboardingPrerequisiteReport report = await onboarding.ValidatePrerequisitesAsync(
                new ValidateOnboardingPrerequisitesRequest
                {
                    NodeId = nodeId,
                    Devices = { PassingFacts(deviceId) },
                },
                headers,
                deadline: Deadline());
            Assert.True(report.Passed);

            Sha256 membership = Utf8Sha256("membership");
            OnboardingPlanSummary plan = await onboarding.CreatePlanAsync(
                PlanRequest(nodeId, deviceId, membership),
                headers,
                deadline: Deadline());
            Assert.Equal(32, plan.PlanHash.Value.Length);
            Assert.NotEmpty(plan.Placements);

            RpcException mismatch = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await onboarding.StartAsync(
                    new StartOnboardingRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        PlanId = plan.PlanId,
                        PlanHash = Utf8Sha256("wrong"),
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.FailedPrecondition, mismatch.StatusCode);

            OnboardingOperationSummary started = await onboarding.StartAsync(
                new StartOnboardingRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    PlanId = plan.PlanId,
                    PlanHash = plan.PlanHash,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(OnboardingOperationState.Committed, started.State);

            List<OnboardingProgress> progress = [];
            using AsyncServerStreamingCall<OnboardingProgress> watch = onboarding.Watch(
                new WatchOnboardingRequest { OperationId = started.OperationId },
                headers,
                deadline: Deadline());
            await foreach (OnboardingProgress item in watch.ResponseStream.ReadAllAsync())
            {
                progress.Add(item);
            }

            Assert.NotEmpty(progress);
            Assert.Equal(OnboardingOperationState.Committed, progress[^1].State);

            OnboardingRecoveryStatus status = await onboarding.GetRecoveryStatusAsync(
                new GetOnboardingRecoveryStatusRequest
                {
                    NodeId = nodeId,
                    OperationId = started.OperationId,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(OnboardingRecoveryAction.KeepManaged, status.Action);
            Assert.Equal("Unmanaged", status.NodeManagementState);
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
                builder.Services.RemoveAll<IOnboardingRuntime>();
                builder.Services.AddSingleton<IOnboardingRuntime>(new ScriptedOnboardingRuntime { Commit = false });
            });
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            OnboardingService.OnboardingServiceClient onboarding = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            Guid planKey = Guid.NewGuid();
            CreateOnboardingPlanRequest planRequest = PlanRequest(nodeId, deviceId, Utf8Sha256("membership"), planKey);
            OnboardingPlanSummary first = await onboarding.CreatePlanAsync(planRequest, headers, deadline: Deadline());
            OnboardingPlanSummary replayed = await onboarding.CreatePlanAsync(planRequest, headers, deadline: Deadline());
            Assert.Equal(first.PlanId, replayed.PlanId);

            OnboardingOperationSummary started = await onboarding.StartAsync(
                new StartOnboardingRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    PlanId = first.PlanId,
                    PlanHash = first.PlanHash,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(OnboardingOperationState.RollbackPending, started.State);

            Guid rollbackKey = Guid.NewGuid();
            RollbackOnboardingRequest rollback = new()
            {
                IdempotencyKey = ProtoUuid.FromGuid(rollbackKey),
                OperationId = started.OperationId,
            };
            OnboardingOperationSummary rolled = await onboarding.RollbackAsync(rollback, headers, deadline: Deadline());
            OnboardingOperationSummary rolledAgain = await onboarding.RollbackAsync(rollback, headers, deadline: Deadline());
            Assert.Equal(OnboardingOperationState.RolledBack, rolled.State);
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
        Site site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "OB1",
                Name = "Onboarding",
            },
            headers,
            deadline: Deadline());
        Node node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "edge",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            },
            headers,
            deadline: Deadline());
        Device device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                DisplayName = "edge",
                ManagementHost = "192.0.2.40",
                ManagementPort = 8729,
                Role = DeviceRole.Router,
            },
            headers,
            deadline: Deadline());
        return (node.Id, device.Id);
    }

    private static CreateOnboardingPlanRequest PlanRequest(
        Uuid nodeId,
        Uuid deviceId,
        Sha256 hash,
        Guid? idempotencyKey = null)
        => new()
        {
            IdempotencyKey = ProtoUuid.FromGuid(idempotencyKey ?? Guid.NewGuid()),
            NodeId = nodeId,
            NodeMembershipHash = hash,
            TopologyProjectionHash = hash,
            Devices =
            {
                new OnboardingDevicePlanInput
                {
                    DeviceId = deviceId,
                    ExpectedRouterosVersion = "7.16.2",
                    ExpectedCapabilityHash = hash,
                    ExpectedConfigurationHash = hash,
                    ExpectedCompatibilityHash = hash,
                    ExpectedApiServiceHash = hash,
                    ExpectedReadAccountHash = hash,
                    ExpectedDeploymentAccountHash = hash,
                    ExpectedDeviceModeHash = hash,
                    ExpectedGuardHash = hash,
                    WatchdogTtlSeconds = 180,
                },
            },
        };

    private static OnboardingDevicePrerequisiteFacts PassingFacts(Uuid deviceId)
        => new()
        {
            DeviceId = deviceId,
            ExactSupportedBuild = true,
            VersionMajor = 7,
            VersionMinor = 16,
            VersionPatch = 2,
            VersionChannel = "stable",
            SupportState = 0,
            PlainApi = new OnboardingIpServiceFacts { Found = true, Disabled = true, Port = 8728 },
            ApiSsl = new OnboardingIpServiceFacts
            {
                Found = true,
                Disabled = false,
                Port = 8729,
                Certificate = "mfc-api",
                MaxSessions = 4,
            },
            ReadAccount = new OnboardingServiceAccountFacts
            {
                Name = "mfc-read",
                GroupName = "mfc-read-group",
                Policies = { "api", "read" },
                AddressPrefixes = { "10.0.0.0/24" },
            },
            DeploymentAccount = new OnboardingServiceAccountFacts
            {
                Name = "mfc-deploy",
                GroupName = "mfc-deploy-group",
                Policies = { "api", "read", "write", "test" },
                AddressPrefixes = { "10.0.0.0/24" },
            },
            DeviceMode = new OnboardingDeviceModeFacts { SchedulerEnabled = true, Flagged = false },
            ExpectedApiSslPort = 8729,
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

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(30);

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
}
