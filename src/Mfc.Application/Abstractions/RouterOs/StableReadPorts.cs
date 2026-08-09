namespace Mfc.Application.Abstractions.RouterOs;

/// <summary>Application-level stable-read outcome codes (M1-19).</summary>
public static class StableReadOutcomeCodes
{
    public const string Accepted = "accepted";
    public const string SnapshotUnstable = "SNAPSHOT_UNSTABLE";
    public const string Canceled = "canceled";
}

/// <summary>
/// Coordinates a stable-read capture for a device.
/// Implementations live in Mfc.RouterOs and must not issue write commands.
/// </summary>
public interface IStableReadCoordinatorPort
{
    Task<StableReadCoordinationResult> CoordinateAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stable-read coordination result. Unstable / canceled results are never complete
/// and must not be persisted as a finished snapshot (M1-19 AC#7).
/// </summary>
public sealed class StableReadCoordinationResult
{
    public required string Outcome { get; init; }

    public required int AttemptsUsed { get; init; }

    /// <summary>Aggregate configuration fingerprint hex when accepted; otherwise null.</summary>
    public string? ConfigurationFingerprintHex { get; init; }

    /// <summary>Opaque discovery payload reference for M1-20 assembly; null unless accepted.</summary>
    public IReadOnlyDictionary<string, string>? DiscoverySectionDigests { get; init; }

    public bool IsComplete =>
        string.Equals(Outcome, StableReadOutcomeCodes.Accepted, StringComparison.Ordinal)
        && DiscoverySectionDigests is not null;
}
