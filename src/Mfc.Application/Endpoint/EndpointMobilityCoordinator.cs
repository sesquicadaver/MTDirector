using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Application.Endpoint;

/// <summary>
/// Applies M7.2-03 mobility rules during presence migration when an active incident assessment exists.
/// </summary>
public static class EndpointMobilityCoordinator
{
    /// <summary>
    /// Builds routing context for a migration, invalidating active assessments and recomputing traces on mobility.
    /// </summary>
    public static EndpointPresenceMigrationPlan PlanMigration(
        EndpointPresenceMigrationResult migration,
        UpsertEndpointPresenceCommand command,
        ResponseAssessment? activeAssessment,
        RoutingAssuranceState? routingState,
        DateTimeOffset processedAt)
    {
        ArgumentNullException.ThrowIfNull(migration);
        ArgumentNullException.ThrowIfNull(command);

        EndpointPresenceInterval opened = migration.OpenedInterval;
        if (!EndpointMobilityHandler.IsMobilityEvent(migration.ClosedInterval, opened)
            || activeAssessment is null
            || !activeAssessment.IsActive)
        {
            EndpointRoutingContext routingContext = EndpointRoutingContextBuilder.Build(
                opened,
                command.CorporateRouteTrace,
                command.InternetRouteTrace,
                command.WazuhRouteTrace);
            return new EndpointPresenceMigrationPlan(
                routingContext,
                InvalidatedAssessment: null,
                EnforcementNodeId: EndpointMobilityHandler.ResolveEnforcementNode(opened),
                AutoDeploySuppressed: false);
        }

        if (command.MobilityProbeTargets is null)
        {
            throw new DomainInvariantException(
                $"Mobility probe targets are required when an active incident assessment exists ({EndpointMobilityCodes.MissingProbeTargets}).");
        }

        if (routingState is null)
        {
            throw new DomainInvariantException(
                $"Routing assurance state is required to recompute mobility traces ({EndpointMobilityCodes.MissingRoutingState}).");
        }

        EndpointMobilityOutcome outcome = EndpointMobilityHandler.ProcessActiveIncidentMobility(
            opened,
            activeAssessment,
            routingState.Configuration,
            routingState.OperationalState,
            command.MobilityProbeTargets,
            processedAt);
        return new EndpointPresenceMigrationPlan(
            outcome.RoutingContext,
            outcome.InvalidatedAssessment,
            outcome.EnforcementNodeId,
            outcome.AutoDeploySuppressed);
    }
}

/// <summary>Planned persistence outcome for one endpoint presence migration.</summary>
public sealed record EndpointPresenceMigrationPlan(
    EndpointRoutingContext RoutingContext,
    ResponseAssessment? InvalidatedAssessment,
    NodeId EnforcementNodeId,
    bool AutoDeploySuppressed);
