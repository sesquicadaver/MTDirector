using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Endpoint;

public sealed class ResolveEndpointAttributionCommand
{
    public required string Actor { get; init; }

    public required EndpointAttributionQuery Query { get; init; }

    public required EndpointAttributionSnapshot Snapshot { get; init; }
}

/// <summary>Thin application port for endpoint attribution (M7.2-01).</summary>
public sealed class ResolveEndpointAttributionUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public ResolveEndpointAttributionUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<EndpointAttributionView>> ExecuteAsync(
        ResolveEndpointAttributionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Query);
        ArgumentNullException.ThrowIfNull(command.Snapshot);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(command.Query, command.Snapshot);
        return ApplicationResults.Ok(EndpointAttributionView.FromResult(result));
    }
}
