using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Endpoint;

public sealed class UpsertEndpointPresenceCommand
{
    public required string Actor { get; init; }

    public required Guid EndpointId { get; init; }

    public required EndpointAttributionQuery Query { get; init; }

    public required EndpointAttributionSnapshot Snapshot { get; init; }

    public RouteResolutionTrace? CorporateRouteTrace { get; init; }

    public RouteResolutionTrace? InternetRouteTrace { get; init; }

    public RouteResolutionTrace? WazuhRouteTrace { get; init; }

    public string? Vrf { get; init; }

    /// <summary>Device whose routing assurance snapshots are used to recompute traces on mobility (M7.2-03).</summary>
    public Guid? MobilityRoutingDeviceId { get; init; }

    /// <summary>Corporate/internet/Wazuh destinations for mobility trace recompute (M7.2-03).</summary>
    public EndpointMobilityProbeTargets? MobilityProbeTargets { get; init; }
}

public sealed class GetEndpointRoutingContextQuery
{
    public required string Actor { get; init; }

    public required Guid EndpointId { get; init; }

    public DateTimeOffset? AsOfUtc { get; init; }
}

/// <summary>
/// Opens or migrates endpoint presence from attribution + optional route traces (M7.2-02).
/// </summary>
public sealed class OpenEndpointPresenceUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IEndpointPresenceStore _presence;
    private readonly IResponseAssessmentStore _assessments;
    private readonly IRoutingAssuranceStateStore _routingStates;
    private readonly IClock _clock;

    public OpenEndpointPresenceUseCase(
        IAuthorizationBoundary auth,
        IEndpointPresenceStore presence,
        IResponseAssessmentStore assessments,
        IRoutingAssuranceStateStore routingStates,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(assessments);
        ArgumentNullException.ThrowIfNull(routingStates);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _presence = presence;
        _assessments = assessments;
        _routingStates = routingStates;
        _clock = clock;
    }

    public async Task<ApplicationResult<EndpointPresenceUpsertResultView>> ExecuteAsync(
        UpsertEndpointPresenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Query);
        ArgumentNullException.ThrowIfNull(command.Snapshot);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        EndpointId endpointId = new(command.EndpointId);
        EndpointAttributionResult attribution = EndpointAttributionResolver.Resolve(command.Query, command.Snapshot);
        DateTimeOffset validFrom = _clock.UtcNow;
        EndpointPresenceInterval? active = await _presence.GetActiveIntervalAsync(endpointId, cancellationToken)
            .ConfigureAwait(false);
        EndpointPresenceMigrationResult migration = EndpointPresenceInterval.Open(
            endpointId,
            active,
            attribution,
            command.Query,
            validFrom,
            command.Vrf);
        ResponseAssessment? activeAssessment = await _assessments
            .GetActiveByEndpointAsync(endpointId, cancellationToken)
            .ConfigureAwait(false);
        RoutingAssuranceState? routingState = command.MobilityRoutingDeviceId is Guid deviceId
            ? await _routingStates.GetAsync(new DeviceId(deviceId), cancellationToken).ConfigureAwait(false)
            : null;
        EndpointPresenceMigrationPlan plan = EndpointMobilityCoordinator.PlanMigration(
            migration,
            command,
            activeAssessment,
            routingState,
            validFrom);
        if (plan.InvalidatedAssessment is not null)
        {
            await _assessments.SaveAsync(plan.InvalidatedAssessment, cancellationToken).ConfigureAwait(false);
        }

        await _presence.SaveMigrationAsync(
            migration.ClosedInterval,
            migration.OpenedInterval,
            plan.RoutingContext,
            cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(new EndpointPresenceUpsertResultView
        {
            RoutingContext = EndpointRoutingContextView.FromDomain(plan.RoutingContext),
            InvalidatedAssessment = plan.InvalidatedAssessment is null
                ? null
                : ResponseAssessmentView.FromDomain(plan.InvalidatedAssessment),
            EnforcementNodeId = plan.EnforcementNodeId.Value,
            AutoDeploySuppressed = plan.AutoDeploySuppressed,
        });
    }
}

/// <summary>Reads endpoint routing context by endpoint_id with optional as-of time (M7.2-02).</summary>
public sealed class GetEndpointRoutingContextUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IEndpointPresenceStore _presence;
    private readonly IClock _clock;

    public GetEndpointRoutingContextUseCase(
        IAuthorizationBoundary auth,
        IEndpointPresenceStore presence,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _presence = presence;
        _clock = clock;
    }

    public async Task<ApplicationResult<EndpointRoutingContextView>> ExecuteAsync(
        GetEndpointRoutingContextQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        EndpointId endpointId = new(query.EndpointId);
        EndpointRoutingContext? context = query.AsOfUtc is null
            ? await ResolveCurrentAsync(endpointId, cancellationToken).ConfigureAwait(false)
            : await _presence.GetRoutingContextAsOfAsync(endpointId, query.AsOfUtc.Value, cancellationToken)
                .ConfigureAwait(false);
        if (context is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Endpoint routing context for '{query.EndpointId}' not found."));
        }

        return ApplicationResults.Ok(EndpointRoutingContextView.FromDomain(context));
    }

    private async Task<EndpointRoutingContext?> ResolveCurrentAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken)
    {
        EndpointPresenceInterval? active = await _presence.GetActiveIntervalAsync(endpointId, cancellationToken)
            .ConfigureAwait(false);
        return active is null
            ? null
            : await _presence.GetRoutingContextAsync(active.PresenceId, cancellationToken).ConfigureAwait(false);
    }
}
