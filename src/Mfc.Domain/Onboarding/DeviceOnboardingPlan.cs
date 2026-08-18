using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Per-device slice of an immutable onboarding plan (Onboarding Spec §25).</summary>
public sealed class DeviceOnboardingPlan
{
    private DeviceOnboardingPlan(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedApiServiceHash,
        Hash256 expectedReadAccountHash,
        Hash256 expectedDeploymentAccountHash,
        Hash256 expectedDeviceModeHash,
        Hash256 expectedGuardHash,
        IReadOnlyList<AnchorKey> requiredAnchorSet,
        IReadOnlyList<AnchorPlacement> anchorPlacements,
        Hash256 bootstrapArtifactHash,
        TimeSpan watchdogTtl)
    {
        DeviceId = deviceId;
        ExpectedRouterOsVersion = expectedRouterOsVersion;
        ExpectedCapabilityHash = expectedCapabilityHash;
        ExpectedConfigurationHash = expectedConfigurationHash;
        ExpectedCompatibilityHash = expectedCompatibilityHash;
        ExpectedApiServiceHash = expectedApiServiceHash;
        ExpectedReadAccountHash = expectedReadAccountHash;
        ExpectedDeploymentAccountHash = expectedDeploymentAccountHash;
        ExpectedDeviceModeHash = expectedDeviceModeHash;
        ExpectedGuardHash = expectedGuardHash;
        RequiredAnchorSet = requiredAnchorSet;
        AnchorPlacements = anchorPlacements;
        BootstrapArtifactHash = bootstrapArtifactHash;
        WatchdogTtl = watchdogTtl;
    }

    public DeviceId DeviceId { get; }

    public string ExpectedRouterOsVersion { get; }

    public Hash256 ExpectedCapabilityHash { get; }

    public Hash256 ExpectedConfigurationHash { get; }

    public Hash256 ExpectedCompatibilityHash { get; }

    public Hash256 ExpectedApiServiceHash { get; }

    public Hash256 ExpectedReadAccountHash { get; }

    public Hash256 ExpectedDeploymentAccountHash { get; }

    public Hash256 ExpectedDeviceModeHash { get; }

    public Hash256 ExpectedGuardHash { get; }

    public IReadOnlyList<AnchorKey> RequiredAnchorSet { get; }

    public IReadOnlyList<AnchorPlacement> AnchorPlacements { get; }

    public Hash256 BootstrapArtifactHash { get; }

    public TimeSpan WatchdogTtl { get; }

    public static DeviceOnboardingPlan Create(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedApiServiceHash,
        Hash256 expectedReadAccountHash,
        Hash256 expectedDeploymentAccountHash,
        Hash256 expectedDeviceModeHash,
        Hash256 expectedGuardHash,
        IReadOnlyList<AnchorKey> requiredAnchorSet,
        IReadOnlyList<AnchorPlacement> anchorPlacements,
        Hash256? bootstrapArtifactHash = null,
        TimeSpan? watchdogTtl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRouterOsVersion);
        ArgumentNullException.ThrowIfNull(expectedCapabilityHash);
        ArgumentNullException.ThrowIfNull(expectedConfigurationHash);
        ArgumentNullException.ThrowIfNull(expectedCompatibilityHash);
        ArgumentNullException.ThrowIfNull(expectedApiServiceHash);
        ArgumentNullException.ThrowIfNull(expectedReadAccountHash);
        ArgumentNullException.ThrowIfNull(expectedDeploymentAccountHash);
        ArgumentNullException.ThrowIfNull(expectedDeviceModeHash);
        ArgumentNullException.ThrowIfNull(expectedGuardHash);
        ArgumentNullException.ThrowIfNull(requiredAnchorSet);
        ArgumentNullException.ThrowIfNull(anchorPlacements);

        TimeSpan ttl = watchdogTtl ?? OnboardingCodes.DefaultWatchdogTtl;
        if (ttl < OnboardingCodes.MinWatchdogTtl || ttl > OnboardingCodes.MaxWatchdogTtl)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.WatchdogTtlOutOfRange}: watchdog_ttl must be {OnboardingCodes.MinWatchdogTtl.TotalSeconds}–{OnboardingCodes.MaxWatchdogTtl.TotalSeconds}s.");
        }

        if (requiredAnchorSet.Count == 0)
        {
            throw new DomainInvariantException("required_anchor_set must be non-empty.");
        }

        if (anchorPlacements.Count == 0)
        {
            throw new DomainInvariantException("anchor_placements must be non-empty.");
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (AnchorKey key in requiredAnchorSet)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (!keys.Add(key.Marker))
            {
                throw new DomainInvariantException($"Duplicate required anchor '{key.Marker}'.");
            }
        }

        HashSet<string> placementKeys = new(StringComparer.Ordinal);
        foreach (AnchorPlacement placement in anchorPlacements)
        {
            ArgumentNullException.ThrowIfNull(placement);
            if (!keys.Contains(placement.Key.Marker))
            {
                throw new DomainInvariantException(
                    $"Placement '{placement.Key.Marker}' is not in required_anchor_set.");
            }

            if (!placementKeys.Add(placement.Key.Marker))
            {
                throw new DomainInvariantException($"Duplicate placement for '{placement.Key.Marker}'.");
            }
        }

        if (placementKeys.Count != keys.Count)
        {
            throw new DomainInvariantException("Every required anchor must have exactly one placement.");
        }

        Hash256 bootstrap = bootstrapArtifactHash ?? BootstrapArtifact.Hash;
        if (!bootstrap.Equals(BootstrapArtifact.Hash))
        {
            throw new DomainInvariantException(
                "Device plan bootstrap_artifact_hash must equal the Spec §23 pass-through artifact.");
        }

        if (ttl.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new DomainInvariantException("Watchdog TTL must be a whole number of seconds.");
        }

        return new DeviceOnboardingPlan(
            deviceId,
            expectedRouterOsVersion.Trim(),
            expectedCapabilityHash,
            expectedConfigurationHash,
            expectedCompatibilityHash,
            expectedApiServiceHash,
            expectedReadAccountHash,
            expectedDeploymentAccountHash,
            expectedDeviceModeHash,
            expectedGuardHash,
            requiredAnchorSet.OrderBy(static k => k.Marker, StringComparer.Ordinal).ToArray(),
            anchorPlacements.OrderBy(static p => p.Key.Marker, StringComparer.Ordinal).ToArray(),
            bootstrap,
            ttl);
    }

    public static DeviceOnboardingPlan Reconstitute(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedApiServiceHash,
        Hash256 expectedReadAccountHash,
        Hash256 expectedDeploymentAccountHash,
        Hash256 expectedDeviceModeHash,
        Hash256 expectedGuardHash,
        IReadOnlyList<AnchorKey> requiredAnchorSet,
        IReadOnlyList<AnchorPlacement> anchorPlacements,
        Hash256 bootstrapArtifactHash,
        TimeSpan watchdogTtl)
        => Create(
            deviceId,
            expectedRouterOsVersion,
            expectedCapabilityHash,
            expectedConfigurationHash,
            expectedCompatibilityHash,
            expectedApiServiceHash,
            expectedReadAccountHash,
            expectedDeploymentAccountHash,
            expectedDeviceModeHash,
            expectedGuardHash,
            requiredAnchorSet,
            anchorPlacements,
            bootstrapArtifactHash,
            watchdogTtl);
}
