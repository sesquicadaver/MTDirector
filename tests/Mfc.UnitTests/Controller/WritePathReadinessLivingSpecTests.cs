using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Controller;
using Mfc.RouterOs.DependencyInjection;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Jobs;
using Mfc.RouterOs.Onboarding;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>Living Spec for P2-10 / issue #296 — write-path RouterOS DI gate.</summary>
public sealed class WritePathReadinessLivingSpecTests
{
    [Fact]
    public void Ac1WriteDisabledByDefaultResolvesNotConfiguredWritePorts()
    {
        using HostScope host = BuildHost(writeEnabled: false);
        IOnboardingRuntime onboarding = host.Services.GetRequiredService<IOnboardingRuntime>();
        IDeploymentRuntime deployment = host.Services.GetRequiredService<IDeploymentRuntime>();
        IWatchdogResidueCleanupPort cleanup = host.Services.GetRequiredService<IWatchdogResidueCleanupPort>();
        Assert.IsType<NotConfiguredOnboardingRuntime>(onboarding);
        Assert.IsType<NotConfiguredDeploymentRuntime>(deployment);
        Assert.IsType<NotConfiguredWatchdogResidueCleanupPort>(cleanup);
    }

    [Fact]
    public void Ac2WriteEnabledResolvesProductionWritePortsFromScope()
    {
        using HostScope host = BuildHost(writeEnabled: true);
        using IServiceScope scope = host.Services.CreateScope();
        IOnboardingRuntime onboarding = scope.ServiceProvider.GetRequiredService<IOnboardingRuntime>();
        IDeploymentRuntime deployment = scope.ServiceProvider.GetRequiredService<IDeploymentRuntime>();
        IWatchdogResidueCleanupPort cleanup = scope.ServiceProvider.GetRequiredService<IWatchdogResidueCleanupPort>();
        Assert.IsType<RouterOsOnboardingRuntime>(onboarding);
        Assert.IsType<RouterOsDeploymentRuntime>(deployment);
        Assert.IsType<RouterOsWatchdogResidueCleanupPort>(cleanup);
    }

    [Fact]
    public void Ac3WriteEnabledRegistersSessionFactoriesAndArtifactMaterializer()
    {
        using HostScope host = BuildHost(writeEnabled: true);
        using IServiceScope scope = host.Services.CreateScope();
        Assert.IsType<RouterOsOnboardingSessionFactory>(
            scope.ServiceProvider.GetRequiredService<IRouterOsOnboardingSessionFactory>());
        Assert.IsType<RouterOsDeploymentSessionFactory>(
            scope.ServiceProvider.GetRequiredService<IRouterOsDeploymentSessionFactory>());
        Assert.IsType<RouterOsWatchdogResidueSessionFactory>(
            scope.ServiceProvider.GetRequiredService<IRouterOsWatchdogResidueSessionFactory>());
        Assert.IsType<Mfc.Application.Deployment.FilterArtifactStoreDeploymentArtifactMaterializer>(
            scope.ServiceProvider.GetRequiredService<Mfc.Application.Deployment.IDeploymentArtifactMaterializer>());
    }

    [Fact]
    public void Ac4WriteEnabledIsIndependentOfReadEnabled()
    {
        using HostScope host = BuildHost(writeEnabled: true, readEnabled: false);
        using IServiceScope scope = host.Services.CreateScope();
        Assert.IsType<RouterOsOnboardingRuntime>(
            scope.ServiceProvider.GetRequiredService<IOnboardingRuntime>());
        Assert.IsType<Mfc.RouterOs.Ports.ProbeOnlyRouterOsReadPort>(
            scope.ServiceProvider.GetRequiredService<Mfc.Application.Abstractions.RouterOs.IRouterOsReadPort>());
    }

    [Fact]
    public void Ac5RoadmapReferencesAddRouterOsWriteServices()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("AddRouterOsWriteServices", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-10", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6ConfigurationSectionPathAndControllerDocsCoverWriteEnabled()
    {
        Assert.Equal("Mfc:RouterOs", RouterOsServiceCollectionExtensions.ConfigurationSectionPath);
        string path = Path.Combine(RepoRoot(), "docs/operations/controller-configuration.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);
        Assert.Contains("RouterOs:WriteEnabled", content, StringComparison.Ordinal);
        Assert.Contains("AddRouterOsWriteServices", content, StringComparison.Ordinal);
    }

    private static HostScope BuildHost(bool writeEnabled, bool readEnabled = false)
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
            $"--Mfc:RouterOs:Enabled={readEnabled.ToString().ToLowerInvariant()}",
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
