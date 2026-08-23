using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Policy;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class ValidateIncidentDenyOverlayCommand
{
    public required string Actor { get; init; }

    public required PolicyDocument Document { get; init; }

    public Guid? PolicyOwnerNodeId { get; init; }
}

/// <summary>Validates INCIDENT_DENY_OVERLAY documents against pipeline rules (M7.4-01).</summary>
public sealed class ValidateIncidentDenyOverlayUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public ValidateIncidentDenyOverlayUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<IncidentDenyOverlayValidationView>> ExecuteAsync(
        ValidateIncidentDenyOverlayCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Document);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentOverlayValidate,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        string code = IncidentDenyOverlayDocumentGuard.Validate(command.Document);
        if (code != IncidentDenyOverlayCodes.ValidDocument)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(code));
        }

        if (command.PolicyOwnerNodeId is Guid ownerNodeId)
        {
            try
            {
                IncidentDenyOverlayDocumentGuard.EnsureNodeBinding(command.Document, ownerNodeId);
            }
            catch (DomainInvariantException ex)
            {
                return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
            }
        }

        IncidentDenyOverlayMetadata metadata = command.Document.IncidentDenyOverlayMetadata!;
        return ApplicationResults.Ok(IncidentDenyOverlayValidationView.FromDomain(code, metadata));
    }
}
