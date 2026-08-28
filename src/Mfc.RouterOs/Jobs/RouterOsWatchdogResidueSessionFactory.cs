using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Jobs;

/// <summary>Opens a live API-SSL residue-cleanup session for a device (P2-09).</summary>
public interface IRouterOsWatchdogResidueSessionFactory
{
    Task<IRouterOsWatchdogResidueSession> OpenAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);
}

/// <summary>Disposable live session exposing only the residue cleanup channel.</summary>
public interface IRouterOsWatchdogResidueSession : IAsyncDisposable
{
    DeviceId DeviceId { get; }

    IWatchdogResidueCleanupChannel Channel { get; }
}

/// <summary>Production residue session factory using connection profiles + API-SSL.</summary>
public sealed class RouterOsWatchdogResidueSessionFactory : IRouterOsWatchdogResidueSessionFactory
{
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsConnectionMaterializer _materializer;

    public RouterOsWatchdogResidueSessionFactory(
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(materializer);
        _devices = devices;
        _profiles = profiles;
        _materializer = materializer;
    }

    public async Task<IRouterOsWatchdogResidueSession> OpenAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null || !device.Enabled)
        {
            throw new InvalidOperationException($"Enabled device '{deviceId}' was not found.");
        }

        ConnectionProfileReadModel? profile = await _profiles.GetAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            throw new InvalidOperationException($"Connection profile for device '{device.Id}' is missing.");
        }

        RouterOsReadTarget target = new()
        {
            DeviceId = device.Id,
            Endpoint = device.ManagementEndpoint,
            SecretReference = profile.SecretReference,
            TrustMode = profile.TrustMode,
            CaProfileRef = profile.CaProfileRef,
            PinnedSpkiSha256 = profile.PinnedSpkiSha256,
        };

        using RouterOsConnectionMaterial material = await _materializer
            .MaterializeAsync(target, cancellationToken)
            .ConfigureAwait(false);
        using SecretLease password = new(material.Password.Plaintext);
        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        AuthenticatedRosConnection connection = await AuthenticatedRosConnection
            .ConnectAsync(options, cancellationToken)
            .ConfigureAwait(false);
        return new RouterOsWatchdogResidueSession(device.Id, connection);
    }
}

/// <summary>Live residue cleanup session wrapping an authenticated API-SSL connection.</summary>
internal sealed class RouterOsWatchdogResidueSession : IRouterOsWatchdogResidueSession
{
    private AuthenticatedRosConnection? _connection;
    private RouterOsWatchdogResidueCleanupChannel? _channel;
    private bool _disposed;

    public RouterOsWatchdogResidueSession(DeviceId deviceId, AuthenticatedRosConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        DeviceId = deviceId;
        _connection = connection;
        _channel = new RouterOsWatchdogResidueCleanupChannel(connection.Session);
    }

    public DeviceId DeviceId { get; }

    public IWatchdogResidueCleanupChannel Channel
        => _channel ?? throw new ObjectDisposedException(nameof(RouterOsWatchdogResidueSession));

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel = null;
        AuthenticatedRosConnection? connection = _connection;
        _connection = null;
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
