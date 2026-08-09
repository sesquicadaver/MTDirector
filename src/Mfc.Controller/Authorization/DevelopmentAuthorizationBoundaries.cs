using Mfc.Application.Abstractions.Authorization;

namespace Mfc.Controller.Authorization;

/// <summary>Development-only boundary that allows every permission check.</summary>
public sealed class AllowAllAuthorizationBoundary : IAuthorizationBoundary
{
    public Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fail-closed boundary used outside Development until real authentication lands.
/// </summary>
public sealed class DenyAllAuthorizationBoundary : IAuthorizationBoundary
{
    public Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnauthorizedAccessException(
            $"Actor '{actor}' is forbidden from '{permission}' until authentication is configured.");
    }
}
