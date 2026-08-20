using System.Globalization;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Deployment;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Shared fake infrastructure for Issue Set M4-13 acceptance tests.
/// Contains RecordingChannel, FakeRuntime, scripted rollback/recovery and VRRP helpers.
/// </summary>
internal static class DeploymentAcceptanceHarness
{
    /// <summary>Seeds a <see cref="RecordingChannel"/> with the anchor jump rows for a single device plan.</summary>
    public static RecordingChannel SeedChannel(DeviceDeploymentPlan plan, bool toNew)
    {
        RecordingChannel channel = new();
        int id = 1;
        IReadOnlyList<AnchorTarget> targets = toNew ? plan.NewAnchorTargets : plan.OldAnchorTargets;
        foreach (AnchorTarget target in targets)
        {
            string chain = target.Key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => "input",
            };
            DeploymentReadSurface surface = target.Key.Family == IpAddressFamily.IPv4
                ? DeploymentReadSurface.Ipv4Filter
                : DeploymentReadSurface.Ipv6Filter;
            channel.Seed(
                surface,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*" + id.ToString(CultureInfo.InvariantCulture),
                    ["chain"] = chain,
                    ["action"] = "jump",
                    ["jump-target"] = target.JumpTarget,
                    ["comment"] = target.Key.Marker,
                    ["disabled"] = "false",
                });
            id++;
        }

        return channel;
    }

    /// <summary>Overload that reads the first device plan of a <see cref="DeploymentPlan"/>.</summary>
    public static RecordingChannel SeedChannel(DeploymentPlan plan, bool toNew)
        => SeedChannel(plan.DevicePlans[0], toNew);
}

/// <summary>
/// In-memory write channel that records sent commands and simulates read-back.
/// Supports controlled filter-set failure injection via <see cref="FailFilterSetsAfter"/>.
/// </summary>
internal sealed class RecordingChannel : IDeploymentWriteChannel
{
    private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();
    private int _nextId = 1;
    private int _filterSetCount;

    /// <summary>All sent (path, attributes) pairs in send order.</summary>
    public List<(DeploymentWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

    /// <summary>After this many filter-set calls, subsequent set responses return empty (simulating failure).</summary>
    public int FailFilterSetsAfter { get; set; } = int.MaxValue;

    /// <summary>When true, every IPv6 filter-set leaves jump-target unchanged (dual-stack failure injection).</summary>
    public bool FailIpv6FilterSets { get; set; }

    public void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
    {
        if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
        {
            list = [];
            _prints[surface] = list;
        }

        list.Add(new Dictionary<string, string>(row, StringComparer.Ordinal));
    }

    public Dictionary<string, string>? FindAnchor(AnchorKey key)
    {
        DeploymentReadSurface surface = key.Family == IpAddressFamily.IPv4
            ? DeploymentReadSurface.Ipv4Filter
            : DeploymentReadSurface.Ipv6Filter;
        if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
        {
            return null;
        }

        string chain = key.Chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => "input",
        };
        return list.FirstOrDefault(r =>
            string.Equals(r.GetValueOrDefault("comment"), key.Marker, StringComparison.Ordinal)
            && string.Equals(r.GetValueOrDefault("chain"), chain, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> ScriptNames()
        => _prints.GetValueOrDefault(DeploymentReadSurface.Script)?.Select(static r => r["name"]) ?? [];

    public IEnumerable<string> SchedulerNames()
        => _prints.GetValueOrDefault(DeploymentReadSurface.Scheduler)?.Select(static r => r["name"]) ?? [];

    public Task<IReadOnlyDictionary<string, string>> SendAsync(
        DeploymentWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default)
    {
        Sent.Add((path, attributes.ToArray()));
        string fixedPath = DeploymentWritePaths.Fixed(path);
        if (fixedPath.EndsWith("/add", StringComparison.Ordinal))
        {
            DeploymentReadSurface surface = path switch
            {
                DeploymentWritePath.SystemScriptAdd => DeploymentReadSurface.Script,
                DeploymentWritePath.SystemSchedulerAdd => DeploymentReadSurface.Scheduler,
                DeploymentWritePath.Ipv4FilterAdd => DeploymentReadSurface.Ipv4Filter,
                DeploymentWritePath.Ipv6FilterAdd => DeploymentReadSurface.Ipv6Filter,
                DeploymentWritePath.Ipv4AddressListAdd => DeploymentReadSurface.Ipv4AddressList,
                DeploymentWritePath.Ipv6AddressListAdd => DeploymentReadSurface.Ipv6AddressList,
                _ => throw new InvalidOperationException(path.ToString()),
            };
            Dictionary<string, string> row = attributes.ToDictionary(
                static a => a.Key, static a => a.Value, StringComparer.Ordinal);
            row[".id"] = "*" + _nextId.ToString(CultureInfo.InvariantCulture);
            _nextId++;
            Seed(surface, row);
        }
        else if (DeploymentWritePaths.IsFilterSet(path) || path == DeploymentWritePath.SystemSchedulerSet)
        {
            if (DeploymentWritePaths.IsFilterSet(path))
            {
                _filterSetCount++;
                bool failIpv6 = FailIpv6FilterSets && path == DeploymentWritePath.Ipv6FilterSet;
                if (failIpv6 || _filterSetCount > FailFilterSetsAfter)
                {
                    // Return empty dict — jump-target unchanged so read-back sees divergence → RecoveryRequired.
                    return Task.FromResult<IReadOnlyDictionary<string, string>>(
                        new Dictionary<string, string>(StringComparer.Ordinal));
                }
            }

            string id = attributes.Single(static a => a.Key == ".id").Value;
            DeploymentReadSurface surface = path switch
            {
                DeploymentWritePath.SystemSchedulerSet => DeploymentReadSurface.Scheduler,
                DeploymentWritePath.Ipv6FilterSet => DeploymentReadSurface.Ipv6Filter,
                _ => DeploymentReadSurface.Ipv4Filter,
            };
            Dictionary<string, string> row = _prints[surface].Single(r => r[".id"] == id);
            foreach ((string key, string value) in attributes.Where(static a => a.Key != ".id"))
            {
                row[key] = value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
        DeploymentReadSurface surface,
        CancellationToken cancellationToken = default)
    {
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
        => Task.FromResult(new ChannelPingResult { Sent = 3, Received = 3 });
}

/// <summary>
/// Scripted runtime for <see cref="IStandaloneDeploymentDeviceRuntime"/>.
/// Wraps a <see cref="RecordingChannel"/> and an optional watchdog port override.
/// </summary>
internal sealed class FakeRuntime : IStandaloneDeploymentDeviceRuntime, IAsyncDisposable
{
    private readonly RecordingChannel _channel;
    private readonly RouterOsDeploymentSession _session;
    private readonly DeploymentSystemNameFacts? _names;

    public FakeRuntime(
        DeviceId deviceId,
        RecordingChannel channel,
        IDeploymentWatchdogPort? watchdog = null,
        DeploymentSystemNameFacts? names = null)
    {
        DeviceId = deviceId;
        _channel = channel;
        _session = new RouterOsDeploymentSession(channel);
        Watchdog = watchdog ?? new DeploymentWatchdogWriter(_session);
        FreshSessions = new FakeFreshSessionFactory(channel);
        _names = names;
    }

    public DeviceId DeviceId { get; }

    public IRouterOsDeploymentSession Session => _session;

    public IDeploymentWatchdogPort Watchdog { get; private set; }

    public IDeploymentFreshSessionFactory FreshSessions { get; }

    public void ReplaceWatchdog(IDeploymentWatchdogPort watchdog) => Watchdog = watchdog;

    public Task<DeploymentSystemNameFacts> ReadSystemNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_names ?? new DeploymentSystemNameFacts
        {
            ScriptNames = _channel.ScriptNames().ToArray(),
            SchedulerNames = _channel.SchedulerNames().ToArray(),
        });

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}

/// <summary>Returns a fresh <see cref="RouterOsDeploymentSession"/> backed by the same channel.</summary>
internal sealed class FakeFreshSessionFactory : IDeploymentFreshSessionFactory
{
    private readonly RecordingChannel _channel;

    public FakeFreshSessionFactory(RecordingChannel channel) => _channel = channel;

    public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IRouterOsDeploymentSession>(new RouterOsDeploymentSession(_channel));
}

/// <summary>Configurable watchdog port — arm/disarm both succeed by default.</summary>
internal sealed class ScriptedWatchdog : IDeploymentWatchdogPort
{
    public bool ArmSucceeds { get; init; } = true;

    public Task<DeploymentWatchdogExecutionResult> ArmWatchdogAsync(
        DeploymentWatchdogBundle bundle,
        DateTimeOffset routerClock,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new DeploymentWatchdogExecutionResult
        {
            Succeeded = ArmSucceeds,
            Code = ArmSucceeds ? "OK" : DeploymentCodes.WatchdogArmFailed,
            Paths = [],
        });

    public Task<DeploymentWatchdogExecutionResult> DisarmWatchdogAsync(
        DeploymentWatchdogBundle bundle,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new DeploymentWatchdogExecutionResult { Succeeded = true, Code = "OK", Paths = [] });

    public Task<DeploymentWatchdogExecutionResult> CleanupWatchdogAsync(
        DeploymentOperationId deploymentId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new DeploymentWatchdogExecutionResult { Succeeded = true, Code = "OK", Paths = [] });
}

/// <summary>VRRP cluster fixture used by Ac4 and related acceptance tests.</summary>
internal sealed class ScriptedCluster
{
    public ScriptedCluster(params ScriptedMember[] members) => Members = members;

    public ScriptedMember[] Members { get; }
}

/// <summary>
/// Scripted VRRP member runtime — supports role flip, reachability toggle, and peer activation signals.
/// </summary>
internal sealed class ScriptedMember : IVrrpMemberDeploymentRuntime
{
    private VrrpMemberObservedState _state;

    public ScriptedMember(DeviceId deviceId, VrrpMemberObservedState state)
    {
        DeviceId = deviceId;
        _state = state;
    }

    public DeviceId DeviceId { get; }

    public bool Reachable { get; set; } = true;

    public bool Prechecked { get; private set; }

    public bool Staged { get; private set; }

    public bool Activated { get; private set; }

    public int RoleReadCount { get; private set; }

    public bool FlipRoleAfterFirstPeerActivation { get; set; }

    public bool BecomeUnreachableAfterPeerActivation { get; set; }

    public Func<bool>? PeerActivatedSignal { get; set; }

    public Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Reachable);

    public Task<VrrpMemberRoleSnapshot> ReadRoleSnapshotAsync(CancellationToken cancellationToken = default)
    {
        RoleReadCount++;
        MaybeMutateAfterPeer();
        return Task.FromResult(new VrrpMemberRoleSnapshot
        {
            DeviceId = DeviceId,
            HasIndependentRoutedTraffic = false,
            Reachable = Reachable,
            Instances =
            [
                new VrrpInstanceRoleFact
                {
                    Family = IpAddressFamily.IPv4,
                    Vrid = 1,
                    ObservedState = _state,
                },
            ],
        });
    }

    public Task PrecheckAsync(CancellationToken cancellationToken = default)
    {
        Prechecked = true;
        return Task.CompletedTask;
    }

    public Task StageArtifactAsync(CancellationToken cancellationToken = default)
    {
        Staged = true;
        return Task.CompletedTask;
    }

    public Task ArmWatchdogAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        Activated = true;
        return Task.CompletedTask;
    }

    public Task VerifyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisarmWatchdogAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackActivationAsync(CancellationToken cancellationToken = default)
    {
        Activated = false;
        return Task.CompletedTask;
    }

    private void MaybeMutateAfterPeer()
    {
        if (PeerActivatedSignal is null || !PeerActivatedSignal())
        {
            return;
        }

        if (FlipRoleAfterFirstPeerActivation && _state == VrrpMemberObservedState.Master)
        {
            _state = VrrpMemberObservedState.Backup;
        }

        if (BecomeUnreachableAfterPeerActivation)
        {
            Reachable = false;
        }
    }
}

/// <summary>
/// Scripted rollback/recovery device runtime that maintains in-memory jump-targets and scheduler facts.
/// Supports controlled set failure (FailNextSet) and probe failure (ProbeFails).
/// </summary>
internal sealed class ScriptedRollbackRuntime : IDeploymentRollbackDeviceRuntime
{
    public ScriptedRollbackRuntime(
        DeviceId deviceId,
        Dictionary<string, string> jumps,
        Hash256 observedResourceHash)
    {
        DeviceId = deviceId;
        Jumps = jumps;
        ObservedResourceHash = observedResourceHash;
    }

    public DeviceId DeviceId { get; }

    public Dictionary<string, string> Jumps { get; }

    public Hash256 ObservedResourceHash { get; set; }

    public bool FreshOpened { get; private set; }

    public IReadOnlyList<string> SchedulerNames { get; set; } = [];

    public IReadOnlyDictionary<string, bool> SchedulerDisabled { get; set; } =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    public bool FailNextSet { get; set; }

    public bool ProbeFails { get; set; }

    public Task<IReadOnlyDictionary<string, string>> ReadAnchorJumpsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyDictionary<string, string>)new Dictionary<string, string>(Jumps, StringComparer.Ordinal));

    public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken = default)
    {
        if (FailNextSet)
        {
            FailNextSet = false;
            return Task.FromResult(new DeploymentWriteExecutionResult
            {
                Succeeded = false,
                Path = "/ip/firewall/filter/set",
                SentAttributes = [],
                ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
                Error = "set-failed",
            });
        }

        Jumps[write.OwnershipMarker] = write.JumpTarget;
        return Task.FromResult(new DeploymentWriteExecutionResult
        {
            Succeeded = true,
            Path = "/ip/firewall/filter/set",
            SentAttributes = [],
            ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
        });
    }

    public Task<Hash256> ReadManagedResourceHashAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ObservedResourceHash);

    public Task<IDeploymentFreshSessionFactory> CreateFreshSessionFactoryAsync(
        CancellationToken cancellationToken = default)
    {
        FreshOpened = true;
        return Task.FromResult<IDeploymentFreshSessionFactory>(new NullFreshSessionFactory());
    }

    public Task<RouterPingResult> ProbeAsync(DeploymentProbe probe, CancellationToken cancellationToken = default)
        => Task.FromResult(new RouterPingResult
        {
            Outcome = ProbeFails ? RouterPingOutcome.Fail : RouterPingOutcome.Pass,
            Sent = 3,
            Received = ProbeFails ? 0 : 3,
        });

    public Task DisarmAndCleanupWatchdogAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<(IReadOnlyList<string> SchedulerNames, IReadOnlyDictionary<string, bool> SchedulerDisabled)>
        ReadWatchdogSchedulerFactsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((SchedulerNames, SchedulerDisabled));
}

/// <summary>No-op fresh-session factory for rollback path tests that don't need session writes.</summary>
internal sealed class NullFreshSessionFactory : IDeploymentFreshSessionFactory
{
    public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IRouterOsDeploymentSession>(new NullDeploymentSession());
}

/// <summary>
/// Deployment session stub that throws <see cref="NotSupportedException"/> on all write operations.
/// Used in rollback tests that only exercise the fresh-session open handshake.
/// </summary>
internal sealed class NullDeploymentSession : IRouterOsDeploymentSession
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<ActualManagedState> ReadManagedStateAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> AddAddressListEntryAsync(
        AddressListEntryWrite write,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> AddFilterRuleAsync(
        FilterRuleWrite write,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> AddRollbackScriptAsync(
        RollbackScriptWrite write,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> AddRollbackSchedulerAsync(
        RollbackSchedulerWrite write,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> DisableRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> RemoveRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DeploymentWriteExecutionResult> RemoveRollbackScriptAsync(
        RouterOsItemId scriptId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RouterPingResult> PingAsync(RouterPingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new RouterPingResult { Outcome = RouterPingOutcome.Pass, Sent = 3, Received = 3 });
}
