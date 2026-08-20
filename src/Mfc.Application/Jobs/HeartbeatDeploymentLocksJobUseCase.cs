using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Domain.Deployment;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for durable lock heartbeat (M6-03).</summary>
public sealed class HeartbeatDeploymentLocksJobResult
{
    public required int RefreshedCount { get; init; }
}

/// <summary>
/// Refreshes deployment locks owned by this controller instance within the lease window.
/// OnboardingLock persistence is not yet present — deployment locks only (scope cut).
/// </summary>
public sealed class HeartbeatDeploymentLocksJobUseCase
{
    private readonly IDeploymentStore _deployments;
    private readonly IClock _clock;

    public HeartbeatDeploymentLocksJobUseCase(IDeploymentStore deployments, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(clock);
        _deployments = deployments;
        _clock = clock;
    }

    public async Task<ApplicationResult<HeartbeatDeploymentLocksJobResult>> ExecuteAsync(
        string ownerInstanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerInstanceId);
        IReadOnlyList<DeploymentLock> locks = await _deployments
            .ListLocksByOwnerAsync(ownerInstanceId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = _clock.UtcNow;
        int refreshed = 0;
        foreach (DeploymentLock deploymentLock in locks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deploymentLock.IsExpired(now))
            {
                // Expired locks are retained for recovery inspection — do not auto-delete or steal.
                continue;
            }

            deploymentLock.Heartbeat(ownerInstanceId.Trim(), now);
            await _deployments.SaveLockAsync(deploymentLock, cancellationToken).ConfigureAwait(false);
            refreshed++;
        }

        return ApplicationResults.Ok(new HeartbeatDeploymentLocksJobResult { RefreshedCount = refreshed });
    }
}
