using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Deployment;
using Mfc.Application.Onboarding;
using Mfc.Controller;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Onboarding;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DeploymentTestFactory = Mfc.UnitTests.Deployment.DeploymentTestFactory;
using OnboardingTestFactory = Mfc.UnitTests.Onboarding.OnboardingTestFactory;

namespace Mfc.UnitTests.Controller;

/// <summary>Living Spec for P2-11 / issue #297 — write-path pilot runbook + Start* fail-closed behaviour.</summary>
public sealed class WritePathPilotLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ac1WriteDisabledStartRuntimesThrowNotConfiguredMessages()
    {
        using HostScope host = BuildHost(writeEnabled: false);
        IOnboardingRuntime onboarding = host.Services.GetRequiredService<IOnboardingRuntime>();
        IDeploymentRuntime deployment = host.Services.GetRequiredService<IDeploymentRuntime>();
        Assert.IsType<NotConfiguredOnboardingRuntime>(onboarding);
        Assert.IsType<NotConfiguredDeploymentRuntime>(deployment);

        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingPlan onboardingPlan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation onboardingOp = OnboardingOperation.Create(onboardingPlan, UserId.New(), T0);
        InvalidOperationException onboardingEx = await Assert.ThrowsAsync<InvalidOperationException>(
            () => onboarding.ExecuteAsync(node, onboardingPlan, onboardingOp, T0, T0));
        Assert.Equal(NotConfiguredOnboardingRuntime.NotConfiguredMessage, onboardingEx.Message);

        Node deployNode = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan deployPlan = DeploymentTestFactory.PlanFor(deployNode, T0);
        DeploymentOperation deployOp = DeploymentOperation.Create(deployPlan, deployNode, UserId.New(), T0);
        InvalidOperationException deployEx = await Assert.ThrowsAsync<InvalidOperationException>(
            () => deployment.ExecuteAsync(deployNode, deployPlan, deployOp, [], T0));
        Assert.Equal(NotConfiguredDeploymentRuntime.NotConfiguredMessage, deployEx.Message);
    }

    [Fact]
    public void Ac2WriteEnabledResolvesStartUseCasesWithProductionRuntimes()
    {
        using HostScope host = BuildHost(writeEnabled: true);
        using IServiceScope scope = host.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<StartOnboardingUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<StartDeploymentUseCase>());
        Assert.IsType<RouterOsOnboardingRuntime>(
            scope.ServiceProvider.GetRequiredService<IOnboardingRuntime>());
        Assert.IsType<RouterOsDeploymentRuntime>(
            scope.ServiceProvider.GetRequiredService<IDeploymentRuntime>());
    }

    [Fact]
    public void Ac3PilotRunbookDocumentsWritePathChecklist()
    {
        string path = Path.Combine(RepoRoot(), "docs/operations/pilot-runbook.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);
        Assert.Contains("RouterOs:WriteEnabled", content, StringComparison.Ordinal);
        Assert.Contains("Write-path pilot checklist", content, StringComparison.Ordinal);
        Assert.Contains("StartOnboarding", content, StringComparison.Ordinal);
        Assert.Contains("StartDeployment", content, StringComparison.Ordinal);
        Assert.Contains("Rollback", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac4RoadmapReferencesP211WritePathPilot()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("P2-11", roadmap, StringComparison.Ordinal);
        Assert.Contains("pilot-runbook", roadmap, StringComparison.Ordinal);
        Assert.Contains("WritePathPilotLivingSpecTests", roadmap, StringComparison.Ordinal);
    }

    private static HostScope BuildHost(bool writeEnabled)
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
            $"--Mfc:RouterOs:WriteEnabled={writeEnabled.ToString().ToLowerInvariant()}",
        ];

        var app = Program.BuildHost(args);
        return new HostScope(app, app.Services);
    }

    private sealed class HostScope(IDisposable host, IServiceProvider services) : IDisposable
    {
        public IServiceProvider Services { get; } = services;

        public void Dispose() => host.Dispose();
    }

    private static string RepoRoot()
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

        throw new InvalidOperationException("Repository root not found.");
    }
}
