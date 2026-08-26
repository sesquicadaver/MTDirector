using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;

namespace Mfc.RouterOs.Deployment;

/// <summary>Owns live deployment sessions and disposes underlying API-SSL connections.</summary>
public sealed class RouterOsDeploymentScopedSessions : IAsyncDisposable
{
    private readonly IReadOnlyList<IAsyncDisposable> _disposables;

    public RouterOsDeploymentScopedSessions(IReadOnlyList<IDeploymentLiveDeviceSession> sessions)
        : this(sessions, sessions.OfType<IAsyncDisposable>().ToArray())
    {
    }

    internal RouterOsDeploymentScopedSessions(
        IReadOnlyList<IDeploymentLiveDeviceSession> sessions,
        IReadOnlyList<IAsyncDisposable>? disposables = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        Sessions = sessions;
        _disposables = disposables ?? [];
    }

    public IReadOnlyList<IDeploymentLiveDeviceSession> Sessions { get; }

    public async ValueTask DisposeAsync()
    {
        foreach (IAsyncDisposable disposable in _disposables)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
