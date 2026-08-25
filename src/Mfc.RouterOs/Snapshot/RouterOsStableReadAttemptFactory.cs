using Mfc.Application.Abstractions.RouterOs;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Opens a fresh API-SSL session for each stable-read attempt.</summary>
public sealed class RouterOsStableReadAttemptFactory : IStableReadAttemptFactory<RouterOsDiscoveryDataset>
{
    private readonly IRouterOsConnectionMaterializer _materializer;
    private readonly RouterOsReadTarget _target;

    public RouterOsStableReadAttemptFactory(
        IRouterOsConnectionMaterializer materializer,
        RouterOsReadTarget target)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        ArgumentNullException.ThrowIfNull(target);
        _materializer = materializer;
        _target = target;
    }

    public async Task<IStableReadAttemptSession<RouterOsDiscoveryDataset>> OpenAsync(
        CancellationToken cancellationToken)
    {
        RouterOsConnectionMaterial material = await _materializer
            .MaterializeAsync(_target, cancellationToken)
            .ConfigureAwait(false);

        SecretLease password = new(material.Password.Plaintext);
        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        try
        {
            AuthenticatedRosConnection connection = await AuthenticatedRosConnection
                .ConnectAsync(options, cancellationToken)
                .ConfigureAwait(false);
            return new RouterOsStableReadAttemptSession(connection, material);
        }
        catch (ApiSslException ex)
        {
            material.Dispose();
            throw new InvalidOperationException($"RouterOS API-SSL session open failed: {ex.Code}.", ex);
        }
        catch
        {
            material.Dispose();
            throw;
        }
    }
}

internal sealed class RouterOsStableReadAttemptSession : IStableReadAttemptSession<RouterOsDiscoveryDataset>
{
    private readonly AuthenticatedRosConnection _connection;
    private readonly RouterOsConnectionMaterial _material;
    private bool _disposed;

    public RouterOsStableReadAttemptSession(
        AuthenticatedRosConnection connection,
        RouterOsConnectionMaterial material)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(material);
        _connection = connection;
        _material = material;
    }

    public Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
        StableReadExecutionContext context,
        CancellationToken cancellationToken)
        => RosSessionFingerprintReader.ReadAsync(_connection.Session, context, cancellationToken);

    public Task<RouterOsDiscoveryDataset> ReadCompleteDiscoveryDatasetAsync(
        StableReadExecutionContext context,
        CancellationToken cancellationToken)
        => RouterOsDiscoveryReader.ReadAsync(_connection.Session, context, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
        _material.Dispose();
    }
}
