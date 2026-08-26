using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Onboarding;
using Mfc.Controller;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Onboarding;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using OnboardingExecutionLivingSpecTests = Mfc.UnitTests.Onboarding.OnboardingExecutionLivingSpecTests;

namespace Mfc.UnitTests.RouterOs;

/// <summary>Living Spec for P2-07 / issue #293 — production RouterOsOnboardingRuntime.</summary>
public sealed class RouterOsOnboardingRuntimeLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1RouterOsOnboardingRuntimeImplementsOnboardingRuntime()
    {
        Type type = typeof(RouterOsOnboardingRuntime);
        Assert.True(typeof(IOnboardingRuntime).IsAssignableFrom(type));
        Assert.Contains(type.GetConstructors(), c => c.GetParameters().Length == 1);
    }

    [Fact]
    public void Ac2RoadmapLivingSpecRowReferencesRouterOsOnboardingRuntime()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("RouterOsOnboardingRuntime", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-07", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac3RuntimeDelegatesExecuteToBootstrapUseCaseWithInjectedSessions()
    {
        RouterOsOnboardingRuntime runtime = new(new FakeSessionFactory());
        Node node = Onboarding.OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingPlan plan = Onboarding.OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, Mfc.Domain.Policy.Primitives.UserId.New(), T0);

        OnboardingExecutionResult result = await runtime.ExecuteAsync(node, plan, operation, T0, T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.Committed, result.State);
        Assert.True(result.CapturePerformed);
    }

    [Fact]
    public void Ac4RouterOsOnboardingRuntimeLivesInRouterOsAssembly()
    {
        Assert.Equal("Mfc.RouterOs", typeof(RouterOsOnboardingRuntime).Assembly.GetName().Name);
    }

    [Fact]
    public void Ac5ControllerStillRegistersNotConfiguredOnboardingRuntimeUntilWriteGate()
    {
        using HostScope host = BuildHost();
        IOnboardingRuntime runtime = host.Services.GetRequiredService<IOnboardingRuntime>();
        Assert.IsType<NotConfiguredOnboardingRuntime>(runtime);
    }

    [Fact]
    public void Ac6RouterOsOnboardingWriteChannelImplementsOnboardingWriteChannel()
    {
        Assert.True(typeof(IOnboardingWriteChannel).IsAssignableFrom(typeof(RouterOsOnboardingWriteChannel)));
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

    private sealed class FakeSessionFactory : IRouterOsOnboardingSessionFactory
    {
        public Task<RouterOsOnboardingScopedSessions> OpenAsync(
            Node node,
            OnboardingPlan plan,
            CancellationToken cancellationToken = default)
        {
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(
                    plan.DevicePlans[0].DeviceId);
            return Task.FromResult(new RouterOsOnboardingScopedSessions([session]));
        }
    }

    private sealed class HostScope(WebApplication app, IServiceProvider services) : IDisposable
    {
        public IServiceProvider Services { get; } = services;

        public void Dispose() => app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
