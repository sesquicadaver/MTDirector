using Mfc.Application.Abstractions.RouterOs;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Ports;

/// <summary>
/// Production <see cref="IRouterOsReadPort"/> — API-SSL identity probe via allowlisted system discovery (P2-04 / M1-09…M1-11).
/// Also exposes on-demand neighbor table reads for seed MikroTik suggestions (#314).
/// </summary>
public sealed class RouterOsReadPort : IRouterOsReadPort
{
    private readonly IRouterOsConnectionMaterializer _materializer;

    public RouterOsReadPort(IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        _materializer = materializer;
    }

    /// <inheritdoc />
    public async Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        using RouterOsConnectionMaterial material = await _materializer
            .MaterializeAsync(target, cancellationToken)
            .ConfigureAwait(false);

        using Transport.SecretLease password = new(material.Password.Plaintext);

        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        try
        {
            await using AuthenticatedRosConnection connection = await AuthenticatedRosConnection
                .ConnectAsync(options, cancellationToken)
                .ConfigureAwait(false);

            return await RouterOsSystemProbe
                .ProbeAsync(connection.Session, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiSslException ex)
        {
            throw new InvalidOperationException($"RouterOS API-SSL probe failed: {ex.Code}.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<RouterOsNeighborDiscoveryResult> ListNeighborRowsAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        using RouterOsConnectionMaterial material = await _materializer
            .MaterializeAsync(target, cancellationToken)
            .ConfigureAwait(false);

        using Transport.SecretLease password = new(material.Password.Plaintext);

        ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
        try
        {
            await using AuthenticatedRosConnection connection = await AuthenticatedRosConnection
                .ConnectAsync(options, cancellationToken)
                .ConfigureAwait(false);

            RosSession session = connection.Session;
            RosReadCommandResult identity = await RosReadCommandExecutor
                .ExecuteAsync(session, RosReadCommandId.SystemIdentity, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            RosReadCommandResult neighbors = await RosReadCommandExecutor
                .ExecuteAsync(session, RosReadCommandId.IpNeighbors, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (neighbors.Lifecycle != RosCommandLifecycle.Completed || neighbors.Error is not null)
            {
                string detail = neighbors.Error?.Message ?? neighbors.Lifecycle.ToString();
                throw new InvalidOperationException($"RouterOS neighbor read failed: {detail}.");
            }

            return new RouterOsNeighborDiscoveryResult
            {
                SeedIdentity = NeighborDiscoveryMapper.ReadSeedIdentity(identity),
                Rows = NeighborDiscoveryMapper.MapRows(neighbors),
            };
        }
        catch (ApiSslException ex)
        {
            throw new InvalidOperationException($"RouterOS API-SSL neighbor read failed: {ex.Code}.", ex);
        }
    }
}
