using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
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
/// Binds a normalized incident signal to a response assessment per M7.3 contract (M7.3-06)
/// and persists the active assessment for mobility invalidation (AUDIT-M7-01 / F13).
/// </summary>
public sealed class BindIncidentResponseAssessmentUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IResponseAssessmentStore _assessments;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public BindIncidentResponseAssessmentUseCase(
        IAuthorizationBoundary auth,
        IResponseAssessmentStore assessments,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(assessments);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _assessments = assessments;
        _clock = clock;
        _unitOfWork = unitOfWork;
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

            EndpointId endpointId = new(command.EndpointId);
            ResponseAssessment? prior = await _assessments
                .GetActiveByEndpointAsync(endpointId, cancellationToken)
                .ConfigureAwait(false);
            DateTimeOffset now = _clock.UtcNow;
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    if (prior is not null && prior.IsActive)
                    {
                        await _assessments
                            .SaveAsync(
                                prior.Invalidate(now, "superseded_by_new_incident_bind"),
                                ct)
                            .ConfigureAwait(false);
                    }

                    await _assessments.SaveAsync(binding.Assessment, ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

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
