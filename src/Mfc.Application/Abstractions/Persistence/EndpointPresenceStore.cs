using Mfc.Domain.Endpoint;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Persists endpoint presence intervals and routing contexts (M7.2-02).</summary>
public interface IEndpointPresenceStore
{
    Task<EndpointPresenceInterval?> GetActiveIntervalAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default);

    Task<EndpointPresenceInterval?> GetIntervalAsOfAsync(
        EndpointId endpointId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);

    Task<EndpointRoutingContext?> GetRoutingContextAsync(
        PresenceId presenceId,
        CancellationToken cancellationToken = default);

    Task<EndpointRoutingContext?> GetRoutingContextAsOfAsync(
        EndpointId endpointId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);

    Task SaveMigrationAsync(
        EndpointPresenceInterval? closedInterval,
        EndpointPresenceInterval openedInterval,
        EndpointRoutingContext routingContext,
        CancellationToken cancellationToken = default);
}
