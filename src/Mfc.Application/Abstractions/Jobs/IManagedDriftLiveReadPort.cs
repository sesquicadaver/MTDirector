using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.Jobs;

/// <summary>Outcome of a live managed-resource observation for drift polling (AUDIT-DRIFT-01 / F12).</summary>
public sealed class ManagedDriftLiveReadResult
{
    public required bool Succeeded { get; init; }

    /// <summary>Observed managed resource hash hex when the live state could be measured.</summary>
    public string? ObservedManagedResourceHashHex { get; init; }

    /// <summary>
    /// True when live rows verify against the committed artifact body (hash is authoritative).
    /// False when the session succeeded but content/anchors diverge (fail-closed measure).
    /// </summary>
    public bool ContentMatchedExpected { get; init; }

    public string? ErrorCode { get; init; }

    public string? Detail { get; init; }

    public static ManagedDriftLiveReadResult Ok(string observedHashHex)
        => new()
        {
            Succeeded = true,
            ObservedManagedResourceHashHex = observedHashHex,
            ContentMatchedExpected = true,
        };

    public static ManagedDriftLiveReadResult Diverged(string? detail)
        => new()
        {
            Succeeded = true,
            ObservedManagedResourceHashHex = null,
            ContentMatchedExpected = false,
            ErrorCode = "managed_content_diverged",
            Detail = detail,
        };

    public static ManagedDriftLiveReadResult Fail(string errorCode, string detail)
        => new()
        {
            Succeeded = false,
            ContentMatchedExpected = false,
            ErrorCode = errorCode,
            Detail = detail,
        };
}

/// <summary>
/// Live RouterOS read of managed resources for drift polling.
/// Failed reads must not be treated as NoDrift by callers.
/// </summary>
public interface IManagedDriftLiveReadPort
{
    Task<ManagedDriftLiveReadResult> ReadActualManagedResourceAsync(
        DeviceId deviceId,
        Hash256 expectedCommittedArtifactHash,
        CancellationToken cancellationToken = default);
}

/// <summary>Fail-closed stub until a RouterOS adapter is registered.</summary>
public sealed class NotConfiguredManagedDriftLiveReadPort : IManagedDriftLiveReadPort
{
    public const string NotConfiguredCode = "live_read_not_configured";

    public const string NotConfiguredMessage =
        "Managed drift live read is not_configured; inject a RouterOS adapter for live actual hash observation.";

    public Task<ManagedDriftLiveReadResult> ReadActualManagedResourceAsync(
        DeviceId deviceId,
        Hash256 expectedCommittedArtifactHash,
        CancellationToken cancellationToken = default)
    {
        _ = deviceId;
        ArgumentNullException.ThrowIfNull(expectedCommittedArtifactHash);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ManagedDriftLiveReadResult.Fail(NotConfiguredCode, NotConfiguredMessage));
    }
}
