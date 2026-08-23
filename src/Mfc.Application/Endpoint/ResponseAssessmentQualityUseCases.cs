using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Endpoint;

public sealed class EvaluateResponseAssessmentQualityCommand
{
    public required string Actor { get; init; }

    public required ResponseAssessmentQualityInput Input { get; init; }
}

/// <summary>
/// Evaluates visibility_status and confidence for a response assessment context (M7.3-05).
/// </summary>
public sealed class EvaluateResponseAssessmentQualityUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public EvaluateResponseAssessmentQualityUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<ResponseAssessmentQualityResultView>> ExecuteAsync(
        EvaluateResponseAssessmentQualityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Input);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentAssessmentRead,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(command.Input);
            return ApplicationResults.Ok(ResponseAssessmentQualityResultView.FromResult(result));
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
