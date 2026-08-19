using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Onboarding;
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
/// M5-10 onboarding topology acceptance (in-process Controller + Postgres).
/// Live CHR remains optional via testlab provision scripts.
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class OnboardingTopologyAcceptanceTests
{
    private static readonly string[] OnboardingTopologies =
    [
        "standalone",
        "standalone-dual-stack",
        "multi-wan-failover",
        "multi-wan-balanced",
        "vrrp-active-passive",
        "vrrp-split-master",
        "crs-switch",
    ];

    private readonly PostgresFixture _postgres;

    public OnboardingTopologyAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public void TestlabContractsCoverEveryOnboardingTopology()
    {
        foreach (string topology in OnboardingTopologies)
        {
            string path = Path.Combine(RepoRoot, "testlab", "chr", "topologies", topology, "topology.json");
            Assert.True(File.Exists(path), $"Missing onboarding topology contract: {topology}");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            Assert.Equal(topology, root.GetProperty("id").GetString());
            Assert.True(root.GetProperty("credentials").GetProperty("reuseForbidden").GetBoolean());
            string fixtureRelative = root.GetProperty("fixture").GetString()!;
            Assert.True(File.Exists(Path.Combine(RepoRoot, "testlab", "chr", fixtureRelative)));
        }
    }

    [Fact]
    public async Task AllMvpTopologiesValidateCreatePlanStartAndStayUnmanagedOnScriptedRuntime()
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
            Metadata headers = ActorHeaders("acceptance");

            await RunCaseAsync(inventory, onboarding, headers, "OA1", NodeKind.Router, DeclaredUplinkMode.One, DeviceRole.Router, devices: 1, ipv6: false, hostBase: 40);
            await RunCaseAsync(inventory, onboarding, headers, "OA2", NodeKind.Router, DeclaredUplinkMode.One, DeviceRole.Router, devices: 1, ipv6: true, hostBase: 41);
            await RunCaseAsync(inventory, onboarding, headers, "OA3", NodeKind.Router, DeclaredUplinkMode.Failover, DeviceRole.Router, devices: 1, ipv6: false, hostBase: 42);
            await RunCaseAsync(inventory, onboarding, headers, "OA4", NodeKind.Router, DeclaredUplinkMode.Balanced, DeviceRole.Router, devices: 1, ipv6: false, hostBase: 43);
            await RunCaseAsync(inventory, onboarding, headers, "OA5", NodeKind.Vrrp, DeclaredUplinkMode.Failover, DeviceRole.Router, devices: 2, ipv6: false, hostBase: 44);
            await RunCaseAsync(inventory, onboarding, headers, "OA6", NodeKind.Vrrp, DeclaredUplinkMode.Failover, DeviceRole.Router, devices: 2, ipv6: false, hostBase: 46);
            OnboardingPlanSummary crs = await RunCaseAsync(
                inventory,
                onboarding,
                headers,
                "OA7",
                NodeKind.Switch,
                DeclaredUplinkMode.None,
                DeviceRole.L2Switch,
                devices: 1,
                ipv6: true,
                hostBase: 48);
            Assert.DoesNotContain(crs.Placements, static p => p.Chain.Equals("forward", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(crs.Placements, static p => p.Marker == "mfc:anchor:v1:4:i");
            Assert.Contains(crs.Placements, static p => p.Marker == "mfc:anchor:v1:6:o");
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stop.Token);
        }
    }

    private static async Task<OnboardingPlanSummary> RunCaseAsync(
        InventoryService.InventoryServiceClient inventory,
        OnboardingService.OnboardingServiceClient onboarding,
        Metadata headers,
        string code,
        NodeKind kind,
        DeclaredUplinkMode uplink,
        DeviceRole role,
        int devices,
        bool ipv6,
        int hostBase)
    {
        try
        {
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
                    DeclaredKind = kind,
                    DeclaredUplinkMode = uplink,
                },
                headers,
                deadline: Deadline());
            List<Uuid> deviceIds = [];
            for (int i = 0; i < devices; i++)
            {
                Device device = await inventory.RegisterDeviceAsync(
                    new RegisterDeviceRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        NodeId = node.Id,
                        DisplayName = $"{code}-dev-{i}",
                        ManagementHost = $"192.0.2.{hostBase + i}",
                        ManagementPort = 8729,
                        Role = role,
                    },
                    headers,
                    deadline: Deadline());
                deviceIds.Add(device.Id);
            }

            ValidateOnboardingPrerequisitesRequest validate = new() { NodeId = node.Id };
            foreach (Uuid deviceId in deviceIds)
            {
                validate.Devices.Add(PassingFacts(deviceId));
            }

            OnboardingPrerequisiteReport report = await onboarding.ValidatePrerequisitesAsync(
                validate,
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
            };
            foreach (Uuid deviceId in deviceIds)
            {
                planRequest.Devices.Add(new OnboardingDevicePlanInput
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
                    IncludeIpv6 = ipv6,
                    WatchdogTtlSeconds = 180,
                });
            }

            OnboardingPlanSummary plan = await onboarding.CreatePlanAsync(planRequest, headers, deadline: Deadline());
            Assert.Equal(32, plan.PlanHash.Value.Length);
            Assert.NotEmpty(plan.Placements);

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
            return plan;
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException($"{code}: {ex.StatusCode} {ex.Status.Detail}", ex);
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

    private static string RepoRoot
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Repository root not found from test base directory.");
        }
    }
}
