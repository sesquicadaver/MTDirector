using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.Jobs;

/// <summary>Result of a restricted temporary-watchdog residue cleanup (E2E §49).</summary>
public sealed class WatchdogResidueCleanupResult
{
    public required bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> RemovedNames { get; init; }
}

/// <summary>
/// Restricted port: may remove only temporary disabled watchdog resources.
/// Implementations must refuse firewall artifacts, snapshots, and audit (fail-closed).
/// </summary>
public interface IWatchdogResidueCleanupPort
{
    Task<WatchdogResidueCleanupResult> RemoveDisabledTemporaryWatchdogResourcesAsync(
        DeviceId deviceId,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken = default);
}

/// <summary>Default fail-closed stub until a RouterOS adapter is registered.</summary>
public sealed class NotConfiguredWatchdogResidueCleanupPort : IWatchdogResidueCleanupPort
{
    public const string NotConfiguredMessage =
        "Watchdog residue cleanup runtime is not_configured; inject an adapter for live RouterOS cleanup.";

    public Task<WatchdogResidueCleanupResult> RemoveDisabledTemporaryWatchdogResourcesAsync(
        DeviceId deviceId,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken = default)
    {
        _ = deviceId;
        ArgumentNullException.ThrowIfNull(candidateNames);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }
}
