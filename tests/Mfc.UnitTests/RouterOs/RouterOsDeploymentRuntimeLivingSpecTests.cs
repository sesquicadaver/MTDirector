using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Deployment;
using Mfc.Controller;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Deployment;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DeploymentAcceptanceHarness = Mfc.UnitTests.Deployment.DeploymentAcceptanceHarness;
using DeploymentTestFactory = Mfc.UnitTests.Deployment.DeploymentTestFactory;
using FakeRuntime = Mfc.UnitTests.Deployment.FakeRuntime;
using RecordingChannel = Mfc.UnitTests.Deployment.RecordingChannel;

namespace Mfc.UnitTests.RouterOs;

/// <summary>Living Spec for P2-08 / issue #294 — production RouterOsDeploymentRuntime.</summary>
public sealed class RouterOsDeploymentRuntimeLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1RouterOsDeploymentRuntimeImplementsDeploymentRuntime()
    {
        Type type = typeof(RouterOsDeploymentRuntime);
        Assert.True(typeof(IDeploymentRuntime).IsAssignableFrom(type));
        Assert.Contains(type.GetConstructors(), c => c.GetParameters().Length == 2);
    }

    [Fact]
    public void Ac2RoadmapLivingSpecRowReferencesRouterOsDeploymentRuntime()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("RouterOsDeploymentRuntime", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-08", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac3RuntimeDelegatesExecuteToStandaloneUseCaseWithInjectedSessions()
    {
        RouterOsDeploymentRuntime runtime = new(new FakeSessionFactory(), new AnchorOnlyDeploymentArtifactMaterializer());
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);

        DeploymentWorkflowExecutionResult result = await runtime.ExecuteAsync(
            node,
            plan,
            operation,
            DeploymentTestFactory.CpuPairs(),
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.Committed, result.State);
        Assert.True(result.ActivationStarted);
    }

    [Fact]
    public void Ac4RouterOsDeploymentRuntimeLivesInRouterOsAssembly()
    {
        Assert.Equal("Mfc.RouterOs", typeof(RouterOsDeploymentRuntime).Assembly.GetName().Name);
    }

    [Fact]
    public void Ac5ControllerStillRegistersNotConfiguredDeploymentRuntimeUntilWriteGate()
    {
        using HostScope host = BuildHost();
        IDeploymentRuntime runtime = host.Services.GetRequiredService<IDeploymentRuntime>();
        Assert.IsType<NotConfiguredDeploymentRuntime>(runtime);
    }

    [Fact]
    public void Ac6RouterOsDeploymentWriteChannelImplementsDeploymentWriteChannel()
    {
        Assert.True(typeof(IDeploymentWriteChannel).IsAssignableFrom(typeof(RouterOsDeploymentWriteChannel)));
    }

    private static HostScope BuildHost()
    {
        string[] args =
        [
            "--environment", "Development",
            "--Mfc:Grpc:ListenAddress=http://127.0.0.1:0",
            "--Mfc:Grpc:AllowInsecureLoopback=true",
            "--Mfc:Security:RequireTls=true",
            "--Mfc:Security:MasterKeyProvider=Development",
            "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
            "--Mfc:Database:ConnectionString=Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=test",
            "--Mfc:OperationalJobs:Enabled=false",
            "--Mfc:RouterOs:Enabled=false",
        ];

        WebApplication app = Program.BuildHost(args);
        return new HostScope(app, app.Services);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
               ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FakeSessionFactory : IRouterOsDeploymentSessionFactory
    {
        public Task<RouterOsDeploymentScopedSessions> OpenAsync(
            Node node,
            DeploymentPlan plan,
            DeploymentOperationId operationId,
            CancellationToken cancellationToken = default)
        {
            DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
            RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(devicePlan, toNew: false);
            FakeRuntime fake = new(devicePlan.DeviceId, channel);
            FakeDeploymentLiveSession session = new(fake, devicePlan);
            return Task.FromResult(new RouterOsDeploymentScopedSessions([session]));
        }
    }

    private sealed class FakeDeploymentLiveSession(FakeRuntime runtime, DeviceDeploymentPlan plan)
        : IDeploymentLiveDeviceSession, IAsyncDisposable
    {
        public DeviceId DeviceId => runtime.DeviceId;

        public IRouterOsDeploymentSession Session => runtime.Session;

        public IDeploymentWatchdogPort Watchdog => runtime.Watchdog;

        public IDeploymentFreshSessionFactory FreshSessions => runtime.FreshSessions;

        public Task<DeploymentSystemNameFacts> ReadSystemNamesAsync(CancellationToken cancellationToken = default)
            => runtime.ReadSystemNamesAsync(cancellationToken);

        public Task<IReadOnlyDictionary<string, string>> ReadAnchorJumpsAsync(CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> jumps = plan.OldAnchorTargets
                .ToDictionary(static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal);
            return Task.FromResult((IReadOnlyDictionary<string, string>)jumps);
        }

        public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
            AnchorTargetWrite write,
            CancellationToken cancellationToken = default)
            => runtime.Session.SetAnchorTargetAsync(write, cancellationToken);

        public Task<Hash256> ReadManagedResourceHashAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(plan.NewArtifactHash);

        public Task<IDeploymentFreshSessionFactory> CreateFreshSessionFactoryAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IDeploymentFreshSessionFactory>(runtime.FreshSessions);

        public Task<RouterPingResult> ProbeAsync(DeploymentProbe probe, CancellationToken cancellationToken = default)
        {
            System.Net.IPAddress destination = System.Net.IPAddress.Parse(probe.Destination);
            return runtime.Session.PingAsync(
                new RouterPingRequest(
                    destination,
                    destination.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        ? IpAddressFamily.IPv6
                        : IpAddressFamily.IPv4,
                    probe.TimeoutMilliseconds),
                cancellationToken);
        }

        public Task DisarmAndCleanupWatchdogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<string> SchedulerNames, IReadOnlyDictionary<string, bool> SchedulerDisabled)>
            ReadWatchdogSchedulerFactsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<string>, IReadOnlyDictionary<string, bool>)>(([], new Dictionary<string, bool>(StringComparer.Ordinal)));

        public ValueTask DisposeAsync() => runtime.DisposeAsync();
    }

    private sealed class HostScope(WebApplication app, IServiceProvider services) : IDisposable
    {
        public IServiceProvider Services { get; } = services;

        public void Dispose() => app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
