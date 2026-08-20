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

/// <summary>
/// Allows a configured system actor for operational background jobs; delegates all others.
/// </summary>
public sealed class SystemActorAuthorizationBoundary : IAuthorizationBoundary
{
    private readonly IAuthorizationBoundary _inner;
    private readonly string _systemActor;

    public SystemActorAuthorizationBoundary(IAuthorizationBoundary inner, string systemActor)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemActor);
        _inner = inner;
        _systemActor = systemActor.Trim();
    }

    public Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(actor.Trim(), _systemActor, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return _inner.EnsureAllowedAsync(actor, permission, cancellationToken);
    }
}
