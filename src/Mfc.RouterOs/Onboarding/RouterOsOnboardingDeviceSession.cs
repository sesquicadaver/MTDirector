using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Live RouterOS device session for onboarding execution (P2-07).
/// </summary>
public sealed class RouterOsOnboardingDeviceSession : IOnboardingDeviceSession, IAsyncDisposable
{
    private readonly IRouterOsConnectionMaterializer _materializer;
    private readonly RouterOsReadTarget _target;
    private AuthenticatedRosConnection? _connection;
    private RouterOsOnboardingWriteChannel? _channel;
    private bool _disposed;

    internal RouterOsOnboardingDeviceSession(
        DeviceId deviceId,
        RouterOsReadTarget target,
        IRouterOsConnectionMaterializer materializer,
        AuthenticatedRosConnection connection)
    {
        DeviceId = deviceId;
        _target = target;
        _materializer = materializer;
        _connection = connection;
        _channel = new RouterOsOnboardingWriteChannel(connection.Session);
    }

    public DeviceId DeviceId { get; }

    public IOnboardingBootstrapWritePort Bootstrap => new OnboardingBootstrapWriter(EnsureChannel());

    public IOnboardingWatchdogPort Watchdog => new OnboardingWatchdogWriter(EnsureChannel());

    public async Task<IReadOnlyList<ActualFilterRule>> PrintFilterAsync(CancellationToken cancellationToken = default)
    {
        RosSession session = EnsureSession();
        FirewallFilterDiscoveryResult discovery = await FirewallFilterDiscovery.DiscoverAsync(session, cancellationToken)
            .ConfigureAwait(false);
        return ActualFilterRuleMapper.FromDiscovery(discovery);
    }

    public async Task<OnboardingSystemNameFacts> PrintSystemNamesAsync(CancellationToken cancellationToken = default)
    {
        RouterOsOnboardingWriteChannel channel = EnsureChannel();
        IReadOnlyList<IReadOnlyDictionary<string, string>> scripts = await channel
            .PrintSystemAsync(OnboardingSystemSurface.Script, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<IReadOnlyDictionary<string, string>> schedulers = await channel
            .PrintSystemAsync(OnboardingSystemSurface.Scheduler, cancellationToken)
            .ConfigureAwait(false);
        return new OnboardingSystemNameFacts
        {
            ScriptNames = scripts
                .Select(static row => row.GetValueOrDefault("name"))
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name!)
                .ToArray(),
            SchedulerNames = schedulers
                .Select(static row => row.GetValueOrDefault("name"))
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name!)
                .ToArray(),
            SchedulerDisabled = schedulers
                .Where(static row => row.ContainsKey("name"))
                .ToDictionary(
                    static row => row["name"],
                    static row => row.GetValueOrDefault("disabled") is "yes" or "true" or "1",
                    StringComparer.Ordinal),
        };
    }

    public Task<OnboardingAuxiliarySnapshot> PrintAuxiliaryAsync(CancellationToken cancellationToken = default)
        => OnboardingAuxiliarySnapshotReader.ReadAsync(EnsureSession(), cancellationToken);

    public async Task<bool> ReconnectManagementAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return false;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            _channel = null;
        }

        _connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        _channel = new RouterOsOnboardingWriteChannel(_connection.Session);
        return true;
    }

    public Task<IReadOnlyList<ActualFilterRule>> CaptureStableAsync(CancellationToken cancellationToken = default)
        => OnboardingFilterStableCapture.CaptureAsync(EnsureSession(), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            _channel = null;
        }
    }

    internal static async Task<RouterOsOnboardingDeviceSession> OpenAsync(
        DeviceId deviceId,
        RouterOsReadTarget target,
        IRouterOsConnectionMaterializer materializer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(materializer);
        using RouterOsConnectionMaterial material = await materializer.MaterializeAsync(target, cancellationToken)
            .ConfigureAwait(false);
        using SecretLease password = new(material.Password.Plaintext);
        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        AuthenticatedRosConnection connection = await AuthenticatedRosConnection.ConnectAsync(options, cancellationToken)
            .ConfigureAwait(false);
        return new RouterOsOnboardingDeviceSession(deviceId, target, materializer, connection);
    }

    private async Task<AuthenticatedRosConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        using RouterOsConnectionMaterial material = await _materializer.MaterializeAsync(_target, cancellationToken)
            .ConfigureAwait(false);
        using SecretLease password = new(material.Password.Plaintext);
        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        try
        {
            return await AuthenticatedRosConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiSslException ex)
        {
            throw new InvalidOperationException($"RouterOS management reconnect failed: {ex.Code}.", ex);
        }
    }

    private RosSession EnsureSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return EnsureConnection().Session;
    }

    private RouterOsOnboardingWriteChannel EnsureChannel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _channel ?? throw new InvalidOperationException("Onboarding session is not connected.");
    }

    private AuthenticatedRosConnection EnsureConnection()
        => _connection ?? throw new InvalidOperationException("Onboarding session is not connected.");
}
