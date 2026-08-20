using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Controller;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Mfc.IntegrationTests.Acceptance;

/// <summary>
/// M6-05 AC#1 front-half: inventory → capture → onboarding for standalone and dual-stack
/// (in-process Controller + Postgres; scripted runtimes; OperationalJobs disabled).
/// Policy → deployment lifecycle is covered by UnitTests Living Spec.
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class StandaloneDualStackE2EAcceptanceTests
{
    private readonly PostgresFixture _postgres;

    public StandaloneDualStackE2EAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Theory]
    [InlineData("E2E4", false)]
    [InlineData("E2E6", true)]
    public async Task InventoryCaptureOnboardingSucceedsForStandaloneAndDualStack(string code, bool ipv6)
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        StandaloneVerticalSliceCapturePort capture = new();
        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<ISnapshotCapturePort>();
                builder.Services.AddSingleton<ISnapshotCapturePort>(capture);
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
            SnapshotService.SnapshotServiceClient snapshots = new(channel);
            OnboardingService.OnboardingServiceClient onboarding = new(channel);
            Metadata headers = ActorHeaders("e2e-acceptance");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = code,
                    Name = code,
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = code,
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
                    DisplayName = $"{code}-dev",
                    ManagementHost = ipv6 ? "2001:db8::10" : "192.0.2.70",
                    ManagementPort = 8729,
                    Role = DeviceRole.Router,
                },
                headers,
                deadline: Deadline());
            await inventory.UpdateDeviceConnectionAsync(
                new UpdateDeviceConnectionRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    DeviceId = device.Id,
                    Username = "readonly",
                    PasswordUtf8 = ByteString.CopyFrom(Encoding.UTF8.GetBytes("ephemeral-lab-secret")),
                    TrustMode = CertificateTrustMode.InternalCa,
                    CaProfileRef = "lab-ca",
                    ConnectTimeoutMs = 5000,
                    CommandTimeoutMs = 30_000,
                    MaxResponseBytes = 1_048_576,
                },
                headers,
                deadline: Deadline());

            capture.FilterAction = "accept";
            capture.InterfaceRunning = "true";
            StartCaptureResponse started = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.False(started.Deduplicated);
            SnapshotSummary summary = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = started.CaptureId },
                headers,
                deadline: Deadline());
            Assert.Equal(SnapshotCaptureStatus.Completed, summary.Status);
            Assert.Equal(32, summary.ConfigurationHash.Value.Length);

            OnboardingPrerequisiteReport report = await onboarding.ValidatePrerequisitesAsync(
                new ValidateOnboardingPrerequisitesRequest
                {
                    NodeId = node.Id,
                    Devices = { PassingFacts(device.Id) },
                },
                headers,
                deadline: Deadline());
            Assert.True(report.Passed, string.Join(',', report.Findings.Select(static f => f.Code)));

            Sha256 hash = Utf8Sha256(code);
            CreateOnboardingPlanRequest planRequest = new()
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                NodeMembershipHash = hash,
                TopologyProjectionHash = hash,
                Devices =
                {
                    new OnboardingDevicePlanInput
                    {
                        DeviceId = device.Id,
                        ExpectedRouterosVersion = "7.16.2",
                        ExpectedCapabilityHash = hash,
                        ExpectedConfigurationHash = hash,
                        ExpectedCompatibilityHash = hash,
                        ExpectedApiServiceHash = hash,
                        ExpectedReadAccountHash = hash,
                        ExpectedDeploymentAccountHash = hash,
                        ExpectedDeviceModeHash = hash,
                        ExpectedGuardHash = hash,
                        IncludeIpv6 = ipv6,
                        WatchdogTtlSeconds = 180,
                    },
                },
            };
            OnboardingPlanSummary plan = await onboarding.CreatePlanAsync(planRequest, headers, deadline: Deadline());
            Assert.Equal(32, plan.PlanHash.Value.Length);
            Assert.Contains(plan.Placements, static p => p.Marker == "mfc:anchor:v1:4:i");
            if (ipv6)
            {
                Assert.Contains(plan.Placements, static p => p.Marker == "mfc:anchor:v1:6:i");
            }
            else
            {
                Assert.DoesNotContain(plan.Placements, static p => p.Marker.Contains(":v1:6:", StringComparison.Ordinal));
            }

            OnboardingOperationSummary committed = await onboarding.StartAsync(
                new StartOnboardingRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    PlanId = plan.PlanId,
                    PlanHash = plan.PlanHash,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(OnboardingOperationState.Committed, committed.State);
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stop.Token);
        }
    }

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

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(45);

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
