using System.Net;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Live RouterOS device session for deployment execution, rollback, and recovery (P2-08).
/// </summary>
public sealed class RouterOsDeploymentDeviceSession
    : IDeploymentLiveDeviceSession,
        IAsyncDisposable
{
    private readonly DeviceDeploymentPlan _devicePlan;
    private readonly DeploymentOperationId _operationId;
    private readonly IRouterOsConnectionMaterializer _materializer;
    private readonly RouterOsReadTarget _target;
    private AuthenticatedRosConnection? _connection;
    private RouterOsDeploymentSession? _session;
    private RouterOsDeploymentFreshSessionFactory? _freshSessions;
    private bool _disposed;

    internal RouterOsDeploymentDeviceSession(
        DeviceId deviceId,
        DeviceDeploymentPlan devicePlan,
        DeploymentOperationId operationId,
        RouterOsReadTarget target,
        IRouterOsConnectionMaterializer materializer,
        AuthenticatedRosConnection connection)
    {
        DeviceId = deviceId;
        _devicePlan = devicePlan;
        _operationId = operationId;
        _target = target;
        _materializer = materializer;
        _connection = connection;
        _session = new RouterOsDeploymentSession(new RouterOsDeploymentWriteChannel(connection.Session));
        _freshSessions = new RouterOsDeploymentFreshSessionFactory(_target, _materializer);
    }

    public DeviceId DeviceId { get; }

    public DeploymentOperationId OperationId => _operationId;

    public IRouterOsDeploymentSession Session => EnsureSession();

    public IDeploymentWatchdogPort Watchdog => new DeploymentWatchdogWriter(EnsureSession());

    public IDeploymentFreshSessionFactory FreshSessions
        => _freshSessions ?? throw new InvalidOperationException("Deployment session is not connected.");

    internal RosSession RosSession => EnsureConnection().Session;

    public async Task<DeploymentSystemNameFacts> ReadSystemNamesAsync(CancellationToken cancellationToken = default)
    {
        ActualManagedState state = await EnsureSession().ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        return new DeploymentSystemNameFacts
        {
            ScriptNames = state.Scripts
                .Select(static row => row.GetValueOrDefault("name"))
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name!)
                .ToArray(),
            SchedulerNames = state.Schedulers
                .Select(static row => row.GetValueOrDefault("name"))
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name!)
                .ToArray(),
        };
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadAnchorJumpsAsync(
        CancellationToken cancellationToken = default)
    {
        ActualManagedState state = await EnsureSession().ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> jumps = new(StringComparer.Ordinal);
        foreach (AnchorTarget target in _devicePlan.OldAnchorTargets.Concat(_devicePlan.NewAnchorTargets)
                     .DistinctBy(static t => t.Key.Marker))
        {
            IReadOnlyDictionary<string, string>? row = FindAnchorRow(state, target.Key);
            if (row is not null && row.TryGetValue("jump-target", out string? jump) && !string.IsNullOrWhiteSpace(jump))
            {
                jumps[target.Key.Marker] = jump.Trim();
            }
        }

        return jumps;
    }

    public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken = default)
        => EnsureSession().SetAnchorTargetAsync(write, cancellationToken);

    public async Task<Hash256> ReadManagedResourceHashAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string> jumps = await ReadAnchorJumpsAsync(cancellationToken).ConfigureAwait(false);
        DeploymentAnchorSetState classified = DeploymentRecoveryDecision.ClassifyAnchors(
            _devicePlan.OldAnchorTargets,
            _devicePlan.NewAnchorTargets,
            jumps);
        return classified switch
        {
            DeploymentAnchorSetState.AllNew => _devicePlan.NewArtifactHash,
            DeploymentAnchorSetState.AllOld => _devicePlan.OldArtifactHash,
            _ => _devicePlan.NewArtifactHash,
        };
    }

    public Task<IDeploymentFreshSessionFactory> CreateFreshSessionFactoryAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IDeploymentFreshSessionFactory>(FreshSessions);

    public async Task<RouterPingResult> ProbeAsync(DeploymentProbe probe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);
        IPAddress? destination = IPAddress.TryParse(probe.Destination, out IPAddress? parsed) ? parsed : null;
        if (destination is null)
        {
            return new RouterPingResult
            {
                Outcome = RouterPingOutcome.Fail,
                Sent = 0,
                Received = 0,
                Detail = "Invalid probe destination.",
            };
        }

        IPAddress? source = string.IsNullOrWhiteSpace(probe.SourceAddress)
            ? null
            : IPAddress.TryParse(probe.SourceAddress, out IPAddress? src) ? src : null;
        return await EnsureSession().PingAsync(
            new RouterPingRequest(
                destination,
                destination.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                    ? IpAddressFamily.IPv6
                    : IpAddressFamily.IPv4,
                probe.TimeoutMilliseconds,
                source,
                probe.RoutingTable,
                probe.Interface),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DisarmAndCleanupWatchdogAsync(CancellationToken cancellationToken = default)
    {
        DeploymentWatchdogWriter writer = new(EnsureSession());
        _ = await writer.CleanupWatchdogAsync(_operationId, DeviceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<string> SchedulerNames, IReadOnlyDictionary<string, bool> SchedulerDisabled)>
        ReadWatchdogSchedulerFactsAsync(CancellationToken cancellationToken = default)
    {
        ActualManagedState state = await EnsureSession().ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        string[] names = state.Schedulers
            .Select(static row => row.GetValueOrDefault("name"))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToArray();
        Dictionary<string, bool> disabled = state.Schedulers
            .Where(static row => row.ContainsKey("name"))
            .ToDictionary(
                static row => row["name"],
                static row => row.GetValueOrDefault("disabled") is "yes" or "true" or "1",
                StringComparer.Ordinal);
        return (names, disabled);
    }

    public async Task<VrrpMemberRoleSnapshot> ReadVrrpRoleSnapshotAsync(CancellationToken cancellationToken = default)
    {
        VrrpDiscoveryResult discovery = await VrrpDiscovery.DiscoverAsync(RosSession, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        List<VrrpInstanceRoleFact> instances = discovery.Instances
            .Select(static i => new VrrpInstanceRoleFact
            {
                Family = i.Family == IpAddressFamilyKind.Ipv6 ? IpAddressFamily.IPv6 : IpAddressFamily.IPv4,
                Vrid = i.Vrid,
                ObservedState = i.DomainObservedState,
            })
            .ToList();
        return new VrrpMemberRoleSnapshot
        {
            DeviceId = DeviceId,
            HasIndependentRoutedTraffic = false,
            Reachable = true,
            Instances = instances,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _freshSessions = null;
    }

    internal static async Task<RouterOsDeploymentDeviceSession> OpenAsync(
        DeviceId deviceId,
        DeviceDeploymentPlan devicePlan,
        DeploymentOperationId operationId,
        RouterOsReadTarget target,
        IRouterOsConnectionMaterializer materializer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(materializer);
        using RouterOsConnectionMaterial material = await materializer.MaterializeAsync(target, cancellationToken)
            .ConfigureAwait(false);
        using SecretLease password = new(material.Password.Plaintext);
        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        AuthenticatedRosConnection connection = await AuthenticatedRosConnection.ConnectAsync(options, cancellationToken)
            .ConfigureAwait(false);
        return new RouterOsDeploymentDeviceSession(deviceId, devicePlan, operationId, target, materializer, connection);
    }

    private static IReadOnlyDictionary<string, string>? FindAnchorRow(ActualManagedState state, AnchorKey key)
    {
        IEnumerable<IReadOnlyDictionary<string, string>> rows = key.Family == IpAddressFamily.IPv4
            ? state.Ipv4FilterRules
            : state.Ipv6FilterRules;
        string chain = key.Chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => "input",
        };
        return rows.FirstOrDefault(row =>
            string.Equals(row.GetValueOrDefault("comment"), key.Marker, StringComparison.Ordinal)
            && string.Equals(row.GetValueOrDefault("chain"), chain, StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.GetValueOrDefault("action"), "jump", StringComparison.OrdinalIgnoreCase));
    }

    private RouterOsDeploymentSession EnsureSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _session ?? throw new InvalidOperationException("Deployment session is not connected.");
    }

    private AuthenticatedRosConnection EnsureConnection()
        => _connection ?? throw new InvalidOperationException("Deployment session is not connected.");
}

/// <summary>Opens independent API-SSL deployment sessions for post-activation verification.</summary>
internal sealed class RouterOsDeploymentFreshSessionFactory : IDeploymentFreshSessionFactory
{
    private readonly RouterOsReadTarget _target;
    private readonly IRouterOsConnectionMaterializer _materializer;

    public RouterOsDeploymentFreshSessionFactory(
        RouterOsReadTarget target,
        IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(materializer);
        _target = target;
        _materializer = materializer;
    }

    public async Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
    {
        using RouterOsConnectionMaterial material = await _materializer.MaterializeAsync(_target, cancellationToken)
            .ConfigureAwait(false);
        using SecretLease password = new(material.Password.Plaintext);
        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        AuthenticatedRosConnection connection = await AuthenticatedRosConnection.ConnectAsync(options, cancellationToken)
            .ConfigureAwait(false);
        return new RouterOsDeploymentSession(new RouterOsDeploymentWriteChannel(connection.Session));
    }
}
