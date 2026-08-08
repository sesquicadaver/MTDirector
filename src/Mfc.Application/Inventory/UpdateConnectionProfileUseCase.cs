using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Common;
using Mfc.Application.Models;

namespace Mfc.Application.Inventory;

/// <summary>Delegates to <see cref="IConnectionProfileService"/> without exposing secrets in the result.</summary>
public sealed class UpdateConnectionProfileUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IConnectionProfileService _profiles;

    public UpdateConnectionProfileUseCase(IAuthorizationBoundary auth, IConnectionProfileService profiles)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(profiles);
        _auth = auth;
        _profiles = profiles;
    }

    public async Task<ApplicationResult<ConnectionProfileView>> ExecuteAsync(
        UpsertConnectionProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await AuthorizationGuard.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.ConnectionProfileWrite, cancellationToken)
            .ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            ConnectionProfileView view = await _profiles.UpsertAsync(command, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResults.Ok(view);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}
