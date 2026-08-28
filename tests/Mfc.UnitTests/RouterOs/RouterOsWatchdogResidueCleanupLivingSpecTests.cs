using Mfc.Application.Abstractions.Jobs;
using Mfc.Controller;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

/// <summary>Living Spec for P2-09 / issue #295 — production RouterOsWatchdogResidueCleanupPort.</summary>
public sealed class RouterOsWatchdogResidueCleanupLivingSpecTests
{
    private static readonly DeviceId Device = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    [Fact]
    public void Ac1RouterOsWatchdogResidueCleanupPortImplementsCleanupPort()
    {
        Type type = typeof(RouterOsWatchdogResidueCleanupPort);
        Assert.True(typeof(IWatchdogResidueCleanupPort).IsAssignableFrom(type));
        Assert.Contains(type.GetConstructors(), c => c.GetParameters().Length == 1);
    }

    [Fact]
    public void Ac2RoadmapLivingSpecRowReferencesRouterOsWatchdogResidueCleanupPort()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("RouterOsWatchdogResidueCleanupPort", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-09", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac3PortRemovesAllowlistedDisabledResidueViaInjectedChannel()
    {
        const string scheduler = "mfc-rb-d-0123456789abcdef";
        const string script = "mfc-rb-s-0123456789abcdef";
        RecordingChannel channel = new();
        channel.SeedScheduler(scheduler, ".id", "*1", "disabled", "yes");
        channel.SeedScript(script, ".id", "*2");
        RouterOsWatchdogResidueCleanupPort port = new(new FakeSessionFactory(channel));

        WatchdogResidueCleanupResult result = await port.RemoveDisabledTemporaryWatchdogResourcesAsync(
            Device,
            [scheduler, script]);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal([scheduler, script], result.RemovedNames);
        Assert.Contains(WatchdogResidueWritePath.SystemSchedulerRemove, channel.SentPaths);
        Assert.Contains(WatchdogResidueWritePath.SystemScriptRemove, channel.SentPaths);
        Assert.DoesNotContain(WatchdogResidueWritePath.SystemSchedulerSet, channel.SentPaths);
    }

    [Fact]
    public async Task Ac3bPortDisablesEnabledSchedulerBeforeRemoveAndIsIdempotentForMissing()
    {
        const string scheduler = "mfc-ob-d-fedcba9876543210";
        RecordingChannel channel = new();
        channel.SeedScheduler(scheduler, ".id", "*9", "disabled", "no");
        RouterOsWatchdogResidueCleanupPort port = new(new FakeSessionFactory(channel));

        WatchdogResidueCleanupResult first = await port.RemoveDisabledTemporaryWatchdogResourcesAsync(
            Device,
            [scheduler, "mfc-ob-s-fedcba9876543210"]);
        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.Equal([scheduler], first.RemovedNames);
        Assert.Contains(WatchdogResidueWritePath.SystemSchedulerSet, channel.SentPaths);

        WatchdogResidueCleanupResult second = await port.RemoveDisabledTemporaryWatchdogResourcesAsync(
            Device,
            [scheduler]);
        Assert.True(second.Succeeded, second.ErrorCode);
        Assert.Empty(second.RemovedNames);
    }

    [Fact]
    public async Task Ac3cPortRefusesForbiddenFirewallArtifactNames()
    {
        RouterOsWatchdogResidueCleanupPort port = new(new FakeSessionFactory(new RecordingChannel()));
        WatchdogResidueCleanupResult result = await port.RemoveDisabledTemporaryWatchdogResourcesAsync(
            Device,
            ["mfc4.filter.root"]);
        Assert.False(result.Succeeded);
        Assert.Equal(RouterOsWatchdogResidueCleanupPort.CleanupFailedCode, result.ErrorCode);
        Assert.Empty(result.RemovedNames);
    }

    [Fact]
    public void Ac4RouterOsWatchdogResidueCleanupPortLivesInRouterOsAssembly()
    {
        Assert.Equal("Mfc.RouterOs", typeof(RouterOsWatchdogResidueCleanupPort).Assembly.GetName().Name);
    }

    [Fact]
    public void Ac5ControllerStillRegistersNotConfiguredCleanupPortUntilWriteGate()
    {
        using HostScope host = BuildHost();
        IWatchdogResidueCleanupPort port = host.Services.GetRequiredService<IWatchdogResidueCleanupPort>();
        Assert.IsType<NotConfiguredWatchdogResidueCleanupPort>(port);
    }

    [Fact]
    public void Ac6RouterOsWatchdogResidueCleanupChannelImplementsCleanupChannel()
    {
        Assert.True(typeof(IWatchdogResidueCleanupChannel).IsAssignableFrom(typeof(RouterOsWatchdogResidueCleanupChannel)));
        Assert.Equal("/system/scheduler/remove", WatchdogResidueCleanupPaths.Fixed(WatchdogResidueWritePath.SystemSchedulerRemove));
        Assert.Equal("/system/script/remove", WatchdogResidueCleanupPaths.Fixed(WatchdogResidueWritePath.SystemScriptRemove));
        Assert.Equal("/system/script/print", WatchdogResidueCleanupPaths.Fixed(WatchdogResidueReadSurface.Script));
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

    private sealed class FakeSessionFactory(RecordingChannel channel) : IRouterOsWatchdogResidueSessionFactory
    {
        public Task<IRouterOsWatchdogResidueSession> OpenAsync(
            DeviceId deviceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IRouterOsWatchdogResidueSession>(new FakeSession(deviceId, channel));
    }

    private sealed class FakeSession(DeviceId deviceId, IWatchdogResidueCleanupChannel channel)
        : IRouterOsWatchdogResidueSession
    {
        public DeviceId DeviceId { get; } = deviceId;

        public IWatchdogResidueCleanupChannel Channel { get; } = channel;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingChannel : IWatchdogResidueCleanupChannel
    {
        private readonly Dictionary<string, Dictionary<string, string>> _schedulers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>> _scripts = new(StringComparer.Ordinal);

        public List<WatchdogResidueWritePath> SentPaths { get; } = [];

        public void SeedScheduler(string name, params string[] attrs)
            => _schedulers[name] = ToRow(name, attrs);

        public void SeedScript(string name, params string[] attrs)
            => _scripts[name] = ToRow(name, attrs);

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            WatchdogResidueWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            SentPaths.Add(path);
            string id = attributes.First(static a => a.Key == ".id").Value;
            switch (path)
            {
                case WatchdogResidueWritePath.SystemSchedulerSet:
                    KeyValuePair<string, Dictionary<string, string>> scheduler = _schedulers
                        .First(kv => string.Equals(kv.Value.GetValueOrDefault(".id"), id, StringComparison.Ordinal));
                    foreach (KeyValuePair<string, string> attr in attributes.Where(static a => a.Key != ".id"))
                    {
                        scheduler.Value[attr.Key] = attr.Value;
                    }

                    break;
                case WatchdogResidueWritePath.SystemSchedulerRemove:
                    string? schedulerName = _schedulers
                        .FirstOrDefault(kv => string.Equals(kv.Value.GetValueOrDefault(".id"), id, StringComparison.Ordinal))
                        .Key;
                    if (schedulerName is not null)
                    {
                        _schedulers.Remove(schedulerName);
                    }

                    break;
                case WatchdogResidueWritePath.SystemScriptRemove:
                    string? scriptName = _scripts
                        .FirstOrDefault(kv => string.Equals(kv.Value.GetValueOrDefault(".id"), id, StringComparison.Ordinal))
                        .Key;
                    if (scriptName is not null)
                    {
                        _scripts.Remove(scriptName);
                    }

                    break;
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["ok"] = "true" });
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            WatchdogResidueReadSurface surface,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows = surface switch
            {
                WatchdogResidueReadSurface.Scheduler => _schedulers.Values
                    .Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                    .ToArray(),
                WatchdogResidueReadSurface.Script => _scripts.Values
                    .Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                    .ToArray(),
                _ => throw new InvalidOperationException($"Unsupported surface '{surface}'."),
            };
            return Task.FromResult(rows);
        }

        private static Dictionary<string, string> ToRow(string name, string[] attrs)
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal) { ["name"] = name };
            for (int i = 0; i + 1 < attrs.Length; i += 2)
            {
                row[attrs[i]] = attrs[i + 1];
            }

            return row;
        }
    }

    private sealed class HostScope(WebApplication app, IServiceProvider services) : IDisposable
    {
        public IServiceProvider Services { get; } = services;

        public void Dispose() => app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
