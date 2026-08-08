using Mfc.Application.Abstractions.Authorization;

namespace Mfc.Application.Common;

internal static class AuthorizationGuard
{
    public static async Task<ApplicationError?> EnsureAsync(
        IAuthorizationBoundary auth,
        string actor,
        string permission,
        CancellationToken cancellationToken)
    {
        try
        {
            await auth.EnsureAllowedAsync(actor, permission, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApplicationError.Forbidden(ex.Message);
        }
    }
}
