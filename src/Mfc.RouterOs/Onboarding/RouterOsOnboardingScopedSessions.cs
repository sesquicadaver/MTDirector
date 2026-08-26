using Mfc.Application.Onboarding;

namespace Mfc.RouterOs.Onboarding;

/// <summary>Owns live onboarding sessions and disposes underlying API-SSL connections.</summary>
public sealed class RouterOsOnboardingScopedSessions : IAsyncDisposable
{
    private readonly IReadOnlyList<IAsyncDisposable> _disposables;

    public RouterOsOnboardingScopedSessions(IReadOnlyList<IOnboardingDeviceSession> sessions)
        : this(sessions, null)
    {
    }

    internal RouterOsOnboardingScopedSessions(
        IReadOnlyList<IOnboardingDeviceSession> sessions,
        IReadOnlyList<IAsyncDisposable>? disposables = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        Sessions = sessions;
        _disposables = disposables ?? [];
    }

    public IReadOnlyList<IOnboardingDeviceSession> Sessions { get; }

    public async ValueTask DisposeAsync()
    {
        foreach (IAsyncDisposable disposable in _disposables)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
