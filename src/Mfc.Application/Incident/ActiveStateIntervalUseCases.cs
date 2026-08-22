using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class ResolveActiveStateIntervalCommand
{
    public required string Actor { get; init; }

    public required ActiveStateIntervalQuery Query { get; init; }

    public required ActiveStateTimelineSnapshot Snapshot { get; init; }
}

/// <summary>Thin application port for historical active-state resolution (M7.3-02).</summary>
public sealed class ResolveActiveStateIntervalUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public ResolveActiveStateIntervalUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<ActiveStateIntervalResultView>> ExecuteAsync(
        ResolveActiveStateIntervalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Query);
        ArgumentNullException.ThrowIfNull(command.Snapshot);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentContextRead,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
                command.Query,
                command.Snapshot);
            return ApplicationResults.Ok(ActiveStateIntervalResultView.FromResult(result));
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
