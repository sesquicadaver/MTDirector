using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class AssessResponseIntentFeasibilityCommand
{
    public required string Actor { get; init; }

    public required ResponseIntentFeasibilityQuery Query { get; init; }
}

/// <summary>Assesses ResponseIntent enforceability via the normative feasibility matrix (M7.4-02).</summary>
public sealed class AssessResponseIntentFeasibilityUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly EmitResponseFeedbackUseCase _feedback;

    public AssessResponseIntentFeasibilityUseCase(
        IAuthorizationBoundary auth,
        EmitResponseFeedbackUseCase feedback)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(feedback);
        _auth = auth;
        _feedback = feedback;
    }

    public async Task<ApplicationResult<ResponseIntentFeasibilityView>> ExecuteAsync(
        AssessResponseIntentFeasibilityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Query);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentResponseAssess,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            ResponseIntentFeasibilityResult result = ResponseIntentFeasibilityMatrix.Assess(command.Query);
            if (result.Feasibility == ResponseAssessmentFeasibility.NotEnforceableByIpFilter)
            {
                await IncidentOverlayFeedbackSupport.EmitAsync(
                    _feedback,
                    command.Actor,
                    ResponseFeedbackEventKind.Blocked,
                    command.Query.Intent.IncidentId.Value,
                    command.Query.Intent.NodeId.Value,
                    [],
                    command.Query.Intent.IdempotencyKey,
                    residualRisk: result.Feasibility.ToString(),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (result.Feasibility is ResponseAssessmentFeasibility.NewConnectionsOnly
                     or ResponseAssessmentFeasibility.Indeterminate)
            {
                await IncidentOverlayFeedbackSupport.EmitAsync(
                    _feedback,
                    command.Actor,
                    ResponseFeedbackEventKind.Planned,
                    command.Query.Intent.IncidentId.Value,
                    command.Query.Intent.NodeId.Value,
                    [],
                    command.Query.Intent.IdempotencyKey,
                    residualRisk: result.Feasibility.ToString(),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return ApplicationResults.Ok(ResponseIntentFeasibilityView.FromResult(command.Query.Intent, result));
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
