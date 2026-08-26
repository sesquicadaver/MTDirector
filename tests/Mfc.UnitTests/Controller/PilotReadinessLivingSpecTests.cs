using Mfc.Application.Abstractions.RouterOs;
using Mfc.Controller;
using Mfc.RouterOs.DependencyInjection;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>Living Spec for P2-06 / issue #282 — production RouterOS DI gate.</summary>
public sealed class PilotReadinessLivingSpecTests
{
    [Fact]
    public void Ac1DisabledByDefaultResolvesProbeOnlyAndNotConfiguredPorts()
    {
        using HostScope host = BuildHost(enabled: false);
        IRouterOsReadPort readPort = host.Services.GetRequiredService<IRouterOsReadPort>();
        ISnapshotCapturePort capturePort = host.Services.GetRequiredService<ISnapshotCapturePort>();
        Assert.IsType<ProbeOnlyRouterOsReadPort>(readPort);
        Assert.IsType<NotConfiguredSnapshotCapturePort>(capturePort);
    }

    [Fact]
    public void Ac2EnabledResolvesProductionPortsFromScope()
    {
        using HostScope host = BuildHost(enabled: true);
        using IServiceScope scope = host.Services.CreateScope();
        IRouterOsReadPort readPort = scope.ServiceProvider.GetRequiredService<IRouterOsReadPort>();
        ISnapshotCapturePort capturePort = scope.ServiceProvider.GetRequiredService<ISnapshotCapturePort>();
        Assert.IsType<RouterOsReadPort>(readPort);
        Assert.IsType<RouterOsSnapshotCapturePort>(capturePort);
    }

    [Fact]
    public void Ac3EnabledRegistersStableReadCoordinatorPort()
    {
        using HostScope host = BuildHost(enabled: true);
        using IServiceScope scope = host.Services.CreateScope();
        IStableReadCoordinatorPort coordinator = scope.ServiceProvider.GetRequiredService<IStableReadCoordinatorPort>();
        Assert.IsType<RouterOsStableReadCoordinatorPort>(coordinator);
    }

    [Fact]
    public void Ac4RoadmapReferencesAddRouterOsProductionServices()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("AddRouterOsProductionServices", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-06", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5ConfigurationSectionPathIsDocumented()
    {
        Assert.Equal("Mfc:RouterOs", RouterOsServiceCollectionExtensions.ConfigurationSectionPath);
    }

    [Fact]
    public void Ac6PilotRunbookExists()
    {
        string path = Path.Combine(RepoRoot(), "docs/operations/pilot-runbook.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);
        Assert.Contains("RouterOs:Enabled", content, StringComparison.Ordinal);
    }

    private static HostScope BuildHost(bool enabled)
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
            $"--Mfc:RouterOs:Enabled={enabled.ToString().ToLowerInvariant()}",
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
