using System.Diagnostics;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Monotonic remaining TTL after watchdog arm (Safe Deployment Spec §§25–26 / AUDIT-DEP-02).
/// </summary>
public sealed class WatchdogTimeBudget
{
    private readonly TimeSpan _ttl;
    private readonly Stopwatch _sinceArm = Stopwatch.StartNew();

    public WatchdogTimeBudget(TimeSpan ttl)
    {
        if (ttl < TimeSpan.Zero)
        {
            throw new DomainInvariantException("Watchdog TTL cannot be negative.");
        }

        _ttl = ttl;
    }

    public TimeSpan Ttl => _ttl;

    public TimeSpan Remaining
    {
        get
        {
            TimeSpan left = _ttl - _sinceArm.Elapsed;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }
}
