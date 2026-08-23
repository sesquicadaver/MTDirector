using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class ResolveIncidentSessionContextCommand
{
    public required string Actor { get; init; }

    public required IncidentSessionContextQuery Query { get; init; }

    public required ConnectionTrackingSnapshot Snapshot { get; init; }
}

/// <summary>
/// On-demand connection-tracking session context for incidents (M7.3-03). No full-table persistence.
/// </summary>
public sealed class ResolveIncidentSessionContextUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public ResolveIncidentSessionContextUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<IncidentSessionContextResultView>> ExecuteAsync(
        ResolveIncidentSessionContextCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Query);
        ArgumentNullException.ThrowIfNull(command.Snapshot);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentSessionRead,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
                command.Query,
                command.Snapshot);
            return ApplicationResults.Ok(IncidentSessionContextResultView.FromResult(result));
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
