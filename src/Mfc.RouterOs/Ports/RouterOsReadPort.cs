using Mfc.Application.Abstractions.RouterOs;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Ports;

/// <summary>
/// Production <see cref="IRouterOsReadPort"/> — API-SSL identity probe via allowlisted system discovery (P2-04 / M1-09…M1-11).
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
}
