using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class BindIncidentResponseAssessmentCommand
{
    public required string Actor { get; init; }

    public required IncidentSignal Signal { get; init; }

    public required Guid EndpointId { get; init; }

    public required Guid PresenceId { get; init; }

    public required Guid EnforcementNodeId { get; init; }

    public required DateTimeOffset AssessedAt { get; init; }

    public SessionVisibilityStatus? SessionVisibility { get; init; }

    public RouteResolutionTrace? RouteTrace { get; init; }

    public ObservedPacketPathClass PacketPathClass { get; init; } = ObservedPacketPathClass.Unknown;

    public ResponseAssessmentFeasibility? FeasibilityOverride { get; init; }
}

/// <summary>
/// Binds a normalized incident signal to a response assessment per M7.3 contract (M7.3-06).
/// </summary>
public sealed class BindIncidentResponseAssessmentUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public BindIncidentResponseAssessmentUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<IncidentResponseAssessmentBindingView>> ExecuteAsync(
        BindIncidentResponseAssessmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Signal);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentAssessmentBind,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            IncidentResponseAssessmentBinding binding = IncidentResponseAssessmentContract.Bind(
                new IncidentResponseAssessmentQuery
                {
                    Signal = command.Signal,
                    EndpointId = new EndpointId(command.EndpointId),
                    PresenceId = new PresenceId(command.PresenceId),
                    EnforcementNodeId = new NodeId(command.EnforcementNodeId),
                    AssessedAt = command.AssessedAt,
                    SessionVisibility = command.SessionVisibility,
                    RouteTrace = command.RouteTrace,
                    PacketPathClass = command.PacketPathClass,
                    FeasibilityOverride = command.FeasibilityOverride,
                });
            return ApplicationResults.Ok(IncidentResponseAssessmentBindingView.FromBinding(binding));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}
