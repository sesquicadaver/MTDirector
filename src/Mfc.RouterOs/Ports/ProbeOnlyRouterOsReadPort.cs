using Mfc.Application.Abstractions.RouterOs;

namespace Mfc.RouterOs.Ports;

/// <summary>
/// Production default <see cref="IRouterOsReadPort"/> when no live RouterOS session wiring is registered.
/// ValidateDeviceConnection / DiscoverDeviceUseCase map the failure to a typed application error.
/// Integration tests replace this via <c>Program.BuildHost(..., configure)</c>.
/// </summary>
public sealed class ProbeOnlyRouterOsReadPort : IRouterOsReadPort
{
    public const string NotConfiguredMessage =
        "RouterOS read port is not_configured for live sessions; inject a probe adapter for ValidateDeviceConnection.";

    /// <inheritdoc />
    public Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }

    /// <inheritdoc />
    public Task<RouterOsNeighborDiscoveryResult> ListNeighborRowsAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }
}
