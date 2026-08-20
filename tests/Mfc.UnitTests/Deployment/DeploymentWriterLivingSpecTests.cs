using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-02 AC 1–12 (Safe Deployment Spec §6–§8 / §33.2 / §55).
/// </summary>
public sealed class DeploymentWriterLivingSpecTests
{
    [Fact]
    public void Ac1WritePathsAreCompileTimeAllowlisted()
    {
        Assert.Equal("/ip/firewall/address-list/add", DeploymentWritePaths.Fixed(DeploymentWritePath.Ipv4AddressListAdd));
        Assert.Equal("/ipv6/firewall/filter/add", DeploymentWritePaths.Fixed(DeploymentWritePath.Ipv6FilterAdd));
        Assert.Equal("/ip/firewall/filter/set", DeploymentWritePaths.Fixed(DeploymentWritePath.Ipv4FilterSet));
        Assert.Equal("/system/script/add", DeploymentWritePaths.Fixed(DeploymentWritePath.SystemScriptAdd));
        Assert.Equal("/system/scheduler/set", DeploymentWritePaths.Fixed(DeploymentWritePath.SystemSchedulerSet));
        Assert.Equal("/ping", DeploymentWritePaths.Fixed(DeploymentWritePath.Ping));
        Assert.Equal(12, Enum.GetValues<DeploymentWritePath>().Length);
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p => DeploymentWritePaths.Fixed(p).Contains("/move", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p => DeploymentWritePaths.Fixed(p).Contains("address-list/set", StringComparison.Ordinal)
                        || DeploymentWritePaths.Fixed(p).Contains("address-list/remove", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p => DeploymentWritePaths.Fixed(p).Contains("filter/remove", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p => DeploymentWritePaths.Fixed(p).Contains("script/run", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac2FilterSetAllowsOnlyAnchorJumpTarget()
    {
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        channel.Seed(
            DeploymentReadSurface.Ipv4Filter,
            Row(
                (".id", "*a1"),
                ("chain", "forward"),
                ("action", "jump"),
                ("comment", AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward).Marker),
                ("jump-target", "mfc4.f.r.old")));
        DeploymentWriteExecutionResult result = await session.SetAnchorTargetAsync(
            new AnchorTargetWrite(IpAddressFamily.IPv4, FilterBuiltInContext.Forward, "mfc4.f.r.new"));
        Assert.True(result.Succeeded);
        Assert.Equal("/ip/firewall/filter/set", result.Path);
        Assert.Equal(2, result.SentAttributes.Count);
        Assert.Contains(result.SentAttributes, static a => a.Key == ".id" && a.Value == "*a1");
        Assert.Contains(result.SentAttributes, static a => a.Key == "jump-target" && a.Value == "mfc4.f.r.new");
        Assert.DoesNotContain(result.SentAttributes, static a => a.Key is "disabled" or "comment" or "action");
        Assert.Equal("mfc4.f.r.new", result.ReadBack["jump-target"]);
    }

    [Fact]
    public async Task Ac3OrdinaryActiveRulesAreNotChangedBySet()
    {
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        channel.Seed(
            DeploymentReadSurface.Ipv4Filter,
            Row(
                (".id", "*x"),
                ("chain", "forward"),
                ("action", "accept"),
                ("comment", "ordinary-managed"),
                ("jump-target", "")));
        DeploymentWriteExecutionResult result = await session.SetAnchorTargetAsync(
            new AnchorTargetWrite(IpAddressFamily.IPv4, FilterBuiltInContext.Forward, "mfc4.f.r.new"));
        Assert.False(result.Succeeded);
        Assert.Empty(channel.Sent);
        Assert.Contains("exactly one permanent anchor", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac4MoveIsNotUsed()
    {
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p => DeploymentWritePaths.Fixed(p).Contains("/move", StringComparison.Ordinal));
        Assert.Null(typeof(RouterOsDeploymentSession).GetMethod("Move"));
        Assert.Null(typeof(RouterOsDeploymentSession).GetMethod("MoveAsync"));
        Assert.Throws<DomainInvariantException>(() =>
            new FilterRuleWrite(
                IpAddressFamily.IPv4,
                "forward",
                "accept",
                additionalMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["move"] = "1" }));
    }

    [Fact]
    public void Ac5FilterRemoveIsAbsentFromDeploymentPath()
    {
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p => DeploymentWritePaths.Fixed(p).EndsWith("/filter/remove", StringComparison.Ordinal));
        Assert.Null(typeof(IRouterOsDeploymentSession).GetMethod("RemoveFilterRuleAsync"));
        Assert.Null(typeof(RouterOsDeploymentSession).GetMethod("RemoveFilterRuleAsync"));
    }

    [Fact]
    public void Ac6AddressListSetAndRemoveAreAbsent()
    {
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p =>
            {
                string fixedPath = DeploymentWritePaths.Fixed(p);
                return fixedPath.Contains("address-list/set", StringComparison.Ordinal)
                       || fixedPath.Contains("address-list/remove", StringComparison.Ordinal);
            });
        Assert.Null(typeof(IRouterOsDeploymentSession).GetMethod("SetAddressListEntryAsync"));
        Assert.Null(typeof(IRouterOsDeploymentSession).GetMethod("RemoveAddressListEntryAsync"));
    }

    [Fact]
    public async Task Ac7ScriptAndSchedulerApisAreTyped()
    {
        await using RouterOsDeploymentSession session = Session(out _);
        const string source = ":put \"mfc-rollback\";";
        Hash256 hash = Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        DeploymentWriteExecutionResult script = await session.AddRollbackScriptAsync(
            new RollbackScriptWrite("mfc-rb-script", source, hash));
        Assert.True(script.Succeeded, script.Error);
        Assert.Equal("/system/script/add", script.Path);
        Assert.Contains(script.SentAttributes, static a => a.Key == "dont-require-permissions" && a.Value == "no");
        Assert.NotNull(script.SessionItemId);

        DeploymentWriteExecutionResult scheduler = await session.AddRollbackSchedulerAsync(
            new RollbackSchedulerWrite("mfc-rb-sched", "mfc-rb-script", startTime: "startup"));
        Assert.True(scheduler.Succeeded, scheduler.Error);
        Assert.Equal("/system/scheduler/add", scheduler.Path);
        Assert.NotNull(scheduler.SessionItemId);

        DeploymentWriteExecutionResult disabled = await session.DisableRollbackSchedulerAsync(scheduler.SessionItemId.Value);
        Assert.True(disabled.Succeeded, disabled.Error);
        Assert.Equal("/system/scheduler/set", disabled.Path);
        Assert.Contains(disabled.SentAttributes, static a => a.Key == "disabled" && a.Value == "yes");
        Assert.DoesNotContain(disabled.SentAttributes, static a => a.Key is "on-event" or "name" or "interval");
        Assert.Equal("yes", disabled.ReadBack["disabled"]);
    }

    [Fact]
    public async Task Ac8PingParametersAreTypedAndBounded()
    {
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        channel.NextPing = new ChannelPingResult { Sent = 3, Received = 3 };
        RouterPingResult pass = await session.PingAsync(
            new RouterPingRequest(IPAddress.Parse("192.0.2.1"), IpAddressFamily.IPv4, timeoutMilliseconds: 500));
        Assert.Equal(RouterPingOutcome.Pass, pass.Outcome);
        Assert.Equal(3, pass.Sent);
        Assert.Equal(3, pass.Received);
        (DeploymentWritePath path, IReadOnlyList<KeyValuePair<string, string>> attributes) =
            Assert.Single(channel.Sent, static s => s.Path == DeploymentWritePath.Ping);
        Assert.Equal(DeploymentWritePath.Ping, path);
        Assert.Contains(attributes, static a => a.Key == "count" && a.Value == "3");
        Assert.Contains(attributes, static a => a.Key == "address" && a.Value == "192.0.2.1");
        Assert.Equal(RouterPingRequest.FixedCount, new RouterPingRequest(
            IPAddress.Parse("192.0.2.1"),
            IpAddressFamily.IPv4).Count);
        Assert.Throws<DomainInvariantException>(() =>
            new RouterPingRequest(IPAddress.Parse("192.0.2.1"), IpAddressFamily.IPv4, timeoutMilliseconds: 10));
        Assert.Null(typeof(IRouterOsDeploymentSession).GetMethod("PingAsync")!
            .GetParameters()
            .FirstOrDefault(static p => p.ParameterType == typeof(string) && p.Name is "host" or "command"));
    }

    [Fact]
    public async Task Ac9ResourceLookupUsesPrintRead()
    {
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        channel.Seed(
            DeploymentReadSurface.Ipv4Filter,
            Row(
                (".id", "*9"),
                ("chain", "input"),
                ("action", "jump"),
                ("comment", AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input).Marker),
                ("jump-target", "old")));
        Assert.Equal(0, channel.PrintCount);
        Assert.True((await session.SetAnchorTargetAsync(
            new AnchorTargetWrite(IpAddressFamily.IPv4, FilterBuiltInContext.Input, "new"))).Succeeded);
        Assert.True(channel.PrintCount >= 2);
        ActualManagedState state = await session.ReadManagedStateAsync();
        Assert.NotEmpty(state.Ipv4FilterRules);
    }

    [Fact]
    public async Task Ac10ItemIdIsSessionScopedOnly()
    {
        await using RouterOsDeploymentSession session = Session(out _);
        DeploymentWriteExecutionResult added = await session.AddFilterRuleAsync(
            new FilterRuleWrite(
                IpAddressFamily.IPv4,
                "mfc4.f.r.detached",
                "return",
                comment: "mfc:s:detached-return:v1"));
        Assert.True(added.Succeeded);
        Assert.NotNull(added.SessionItemId);
        Assert.DoesNotContain(
            typeof(RouterOsItemId).GetProperties(),
            static p => p.Name.Contains("Persist", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(typeof(string), typeof(RouterOsItemId).GetProperty("Value")!.PropertyType);
    }

    [Fact]
    public async Task Ac11EachWriteHasReadBack()
    {
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        DeploymentWriteExecutionResult add = await session.AddAddressListEntryAsync(
            new AddressListEntryWrite(IpAddressFamily.IPv4, "mfc.al.v1", "10.0.0.0/8", "managed"));
        Assert.True(add.Succeeded);
        Assert.NotEmpty(add.ReadBack);
        Assert.True(channel.PrintCount >= 1);
        Assert.Equal("mfc.al.v1", add.ReadBack["list"]);
    }

    [Fact]
    public void Ac12GenericWriterIsAbsent()
    {
        Assert.Null(typeof(RouterOsDeploymentSession).GetMethod("Execute"));
        Assert.DoesNotContain(
            typeof(RouterOsDeploymentSession).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            static m => m.GetParameters().Any(p => p.ParameterType == typeof(string) && p.Name is "command" or "menu" or "path"));
        Assert.Null(typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes()
            .FirstOrDefault(static t => t.Namespace == "Mfc.RouterOs.Write"));
        Assert.Contains(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t == typeof(RouterOsDeploymentSession) && t.Namespace == "Mfc.RouterOs.Deployment");
        Assert.True(typeof(IRouterOsDeploymentSession).IsAssignableFrom(typeof(RouterOsDeploymentSession)));
    }

    private static RouterOsDeploymentSession Session(out RecordingChannel channel)
    {
        channel = new RecordingChannel();
        return new RouterOsDeploymentSession(channel);
    }

    private static Dictionary<string, string> Row(params (string Key, string Value)[] pairs)
    {
        Dictionary<string, string> row = new(StringComparer.Ordinal);
        foreach ((string key, string value) in pairs)
        {
            row[key] = value;
        }

        return row;
    }

    private sealed class RecordingChannel : IDeploymentWriteChannel
    {
        private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();
        private int _nextId = 1;

        public List<(DeploymentWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public int PrintCount { get; private set; }

        public ChannelPingResult? NextPing { get; set; }

        public void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                list = [];
                _prints[surface] = list;
            }

            list.Add(new Dictionary<string, string>(row, StringComparer.Ordinal));
        }

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            DeploymentWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            if (path == DeploymentWritePath.Ping)
            {
                return Task.FromResult<IReadOnlyDictionary<string, string>>(
                    new Dictionary<string, string>(StringComparer.Ordinal));
            }

            ApplyMutation(path, attributes);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            DeploymentReadSurface surface,
            CancellationToken cancellationToken = default)
        {
            PrintCount++;
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
            }

            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>(
                list.Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                    .ToArray());
        }

        public Task<ChannelPingResult> PingAsync(
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((DeploymentWritePath.Ping, attributes.ToArray()));
            return Task.FromResult(NextPing ?? new ChannelPingResult { Sent = 3, Received = 3 });
        }

        private void ApplyMutation(DeploymentWritePath path, IReadOnlyList<KeyValuePair<string, string>> attributes)
        {
            string fixedPath = DeploymentWritePaths.Fixed(path);
            if (fixedPath.EndsWith("/add", StringComparison.Ordinal))
            {
                DeploymentReadSurface surface = SurfaceForAdd(path);
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = "*" + _nextId.ToString(CultureInfo.InvariantCulture);
                _nextId++;
                Seed(surface, row);
                return;
            }

            if (DeploymentWritePaths.IsFilterSet(path) || path == DeploymentWritePath.SystemSchedulerSet)
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                DeploymentReadSurface surface = path == DeploymentWritePath.SystemSchedulerSet
                    ? DeploymentReadSurface.Scheduler
                    : path == DeploymentWritePath.Ipv4FilterSet
                        ? DeploymentReadSurface.Ipv4Filter
                        : DeploymentReadSurface.Ipv6Filter;
                Dictionary<string, string> row = _prints[surface].Single(r => r[".id"] == id);
                foreach ((string key, string value) in attributes.Where(static a => a.Key != ".id"))
                {
                    row[key] = value;
                }

                return;
            }

            if (path is DeploymentWritePath.SystemScriptRemove or DeploymentWritePath.SystemSchedulerRemove)
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                DeploymentReadSurface surface = path == DeploymentWritePath.SystemScriptRemove
                    ? DeploymentReadSurface.Script
                    : DeploymentReadSurface.Scheduler;
                _prints[surface].RemoveAll(r => r[".id"] == id);
            }
        }

        private static DeploymentReadSurface SurfaceForAdd(DeploymentWritePath path)
            => path switch
            {
                DeploymentWritePath.Ipv4AddressListAdd => DeploymentReadSurface.Ipv4AddressList,
                DeploymentWritePath.Ipv6AddressListAdd => DeploymentReadSurface.Ipv6AddressList,
                DeploymentWritePath.Ipv4FilterAdd => DeploymentReadSurface.Ipv4Filter,
                DeploymentWritePath.Ipv6FilterAdd => DeploymentReadSurface.Ipv6Filter,
                DeploymentWritePath.SystemScriptAdd => DeploymentReadSurface.Script,
                DeploymentWritePath.SystemSchedulerAdd => DeploymentReadSurface.Scheduler,
                _ => throw new InvalidOperationException(path.ToString()),
            };
    }
}
