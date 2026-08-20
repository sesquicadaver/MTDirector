namespace Mfc.Controller.Jobs;

/// <summary>
/// Pure planner: given last-run timestamps and options, produces due work with recovery first.
/// Injectable time — no real sleeps required in unit tests.
/// </summary>
public sealed class OperationalJobTickPlanner
{
    public IReadOnlyList<OperationalJobWorkItem> Plan(
        OperationalJobsOptions options,
        DateTimeOffset nowUtc,
        DateTimeOffset? lastRecoveryUtc,
        DateTimeOffset? lastHeartbeatUtc,
        DateTimeOffset? lastExpiredUtc,
        DateTimeOffset? lastCleanupUtc,
        DateTimeOffset? lastDriftUtc,
        IReadOnlyList<(Guid DeviceId, IReadOnlyList<string> CandidateNames)>? cleanupCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<OperationalJobWorkItem> due = [];

        if (options.RecoveryEnabled
            && IsDue(nowUtc, lastRecoveryUtc, options.RecoveryScanIntervalSeconds))
        {
            due.Add(new OperationalJobWorkItem
            {
                Kind = OperationalJobKind.OperationRecovery,
                EnqueuedAtUtc = nowUtc,
            });
        }

        if (IsDue(nowUtc, lastHeartbeatUtc, options.LockHeartbeatIntervalSeconds))
        {
            due.Add(new OperationalJobWorkItem
            {
                Kind = OperationalJobKind.LockHeartbeat,
                EnqueuedAtUtc = nowUtc,
            });
        }

        if (IsDue(nowUtc, lastExpiredUtc, options.ExpiredExceptionIntervalSeconds))
        {
            due.Add(new OperationalJobWorkItem
            {
                Kind = OperationalJobKind.ExpiredExceptionReconciliation,
                EnqueuedAtUtc = nowUtc,
            });
        }

        if (IsDue(nowUtc, lastCleanupUtc, options.CleanupIntervalSeconds))
        {
            if (cleanupCandidates is { Count: > 0 })
            {
                foreach ((Guid deviceId, IReadOnlyList<string> names) in cleanupCandidates)
                {
                    due.Add(new OperationalJobWorkItem
                    {
                        Kind = OperationalJobKind.WatchdogResidueCleanup,
                        EnqueuedAtUtc = nowUtc,
                        DeviceId = deviceId,
                        CandidateNames = names,
                    });
                }
            }
            else
            {
                due.Add(new OperationalJobWorkItem
                {
                    Kind = OperationalJobKind.WatchdogResidueCleanup,
                    EnqueuedAtUtc = nowUtc,
                });
            }
        }

        if (IsDue(nowUtc, lastDriftUtc, options.DriftPollIntervalSeconds))
        {
            due.Add(new OperationalJobWorkItem
            {
                Kind = OperationalJobKind.DriftCapture,
                EnqueuedAtUtc = nowUtc,
            });
        }

        // Deterministic order: recovery before drift (and other lower priorities).
        due.Sort(static (a, b) =>
        {
            int byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.EnqueuedAtUtc.CompareTo(b.EnqueuedAtUtc);
        });
        return due;
    }

    private static bool IsDue(DateTimeOffset nowUtc, DateTimeOffset? lastUtc, int intervalSeconds)
    {
        if (lastUtc is null)
        {
            return true;
        }

        return nowUtc - lastUtc.Value >= TimeSpan.FromSeconds(intervalSeconds);
    }
}
