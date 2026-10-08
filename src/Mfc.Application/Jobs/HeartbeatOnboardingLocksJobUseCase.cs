using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for durable onboarding lock heartbeat (OWN-HB-01 / F01 residual).</summary>
public sealed class HeartbeatOnboardingLocksJobResult
{
    public required int RefreshedCount { get; init; }
}

/// <summary>
/// Refreshes onboarding locks owned by this controller instance within the lease window.
/// Mirrors <see cref="HeartbeatDeploymentLocksJobUseCase"/> so DefaultLockLease cannot expire mid-Execute.
/// </summary>
public sealed class HeartbeatOnboardingLocksJobUseCase
{
    private readonly IOnboardingStore _onboardings;
    private readonly IClock _clock;

    public HeartbeatOnboardingLocksJobUseCase(IOnboardingStore onboardings, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(onboardings);
        ArgumentNullException.ThrowIfNull(clock);
        _onboardings = onboardings;
        _clock = clock;
    }

    public async Task<ApplicationResult<HeartbeatOnboardingLocksJobResult>> ExecuteAsync(
        string ownerInstanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerInstanceId);
        IReadOnlyList<OnboardingLock> locks = await _onboardings
            .ListLocksByOwnerAsync(ownerInstanceId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = _clock.UtcNow;
        int refreshed = 0;
        foreach (OnboardingLock onboardingLock in locks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (onboardingLock.IsExpired(now))
            {
                // Expired locks are retained for recovery inspection — do not auto-delete or steal.
                continue;
            }

            onboardingLock.Heartbeat(ownerInstanceId.Trim(), now);
            await _onboardings.SaveLockAsync(onboardingLock, cancellationToken).ConfigureAwait(false);
            refreshed++;
        }

        return ApplicationResults.Ok(new HeartbeatOnboardingLocksJobResult { RefreshedCount = refreshed });
    }
}
