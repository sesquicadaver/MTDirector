using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.RouterOs.Jobs;

/// <summary>
/// AUDIT-M7-01 / F13: production post-capture caller for routing assurance upsert.
/// Refreshes the assurance shell after every successful capture so viewers/mobility are not DI-only.
/// </summary>
public sealed class RouterOsRoutingAssuranceCaptureProjectionPort : IRoutingAssuranceCaptureProjectionPort
{
    private readonly IRoutingAssuranceStateStore _states;
    private readonly IClock _clock;

    public RouterOsRoutingAssuranceCaptureProjectionPort(
        IRoutingAssuranceStateStore states,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(clock);
        _states = states;
        _clock = clock;
    }

    public async Task<RoutingAssuranceCaptureProjectionResult> ProjectFromCapturePayloadsAsync(
        DeviceId deviceId,
        ReadOnlyMemory<byte> configurationPayload,
        ReadOnlyMemory<byte> observationPayload,
        CancellationToken cancellationToken = default)
    {
        _ = configurationPayload;
        _ = observationPayload;
        cancellationToken.ThrowIfCancellationRequested();

        RoutingAssuranceState state = RoutingAssuranceState.Create(
            deviceId,
            RoutingConfigurationSnapshot.Empty,
            RoutingOperationalSnapshot.Empty,
            _clock.UtcNow);
        await _states.UpsertAsync(state, cancellationToken).ConfigureAwait(false);
        return RoutingAssuranceCaptureProjectionResult.Ok();
    }
}
