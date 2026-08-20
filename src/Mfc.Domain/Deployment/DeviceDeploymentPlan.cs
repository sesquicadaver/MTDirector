using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>Immutable per-device deployment slice (Safe Deployment Spec §9).</summary>
public sealed class DeviceDeploymentPlan
{
    private DeviceDeploymentPlan(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedGuardContextHash,
        Hash256 expectedAnchorContextHash,
        Hash256 oldArtifactHash,
        IReadOnlyList<AnchorTarget> oldAnchorTargets,
        Hash256 newArtifactHash,
        IReadOnlyList<AnchorTarget> newAnchorTargets,
        IReadOnlyList<AnchorKey> anchorActivationOrder,
        IReadOnlyList<AnchorKey> anchorRollbackOrder,
        IReadOnlyList<Hash256> transitionStateHashes,
        TimeSpan rollbackTtl,
        IReadOnlyList<DeploymentProbe> probes)
    {
        DeviceId = deviceId;
        ExpectedRouterOsVersion = expectedRouterOsVersion;
        ExpectedCapabilityHash = expectedCapabilityHash;
        ExpectedConfigurationHash = expectedConfigurationHash;
        ExpectedCompatibilityHash = expectedCompatibilityHash;
        ExpectedGuardContextHash = expectedGuardContextHash;
        ExpectedAnchorContextHash = expectedAnchorContextHash;
        OldArtifactHash = oldArtifactHash;
        OldAnchorTargets = oldAnchorTargets;
        NewArtifactHash = newArtifactHash;
        NewAnchorTargets = newAnchorTargets;
        AnchorActivationOrder = anchorActivationOrder;
        AnchorRollbackOrder = anchorRollbackOrder;
        TransitionStateHashes = transitionStateHashes;
        RollbackTtl = rollbackTtl;
        Probes = probes;
    }

    public DeviceId DeviceId { get; }

    public string ExpectedRouterOsVersion { get; }

    public Hash256 ExpectedCapabilityHash { get; }

    public Hash256 ExpectedConfigurationHash { get; }

    public Hash256 ExpectedCompatibilityHash { get; }

    public Hash256 ExpectedGuardContextHash { get; }

    public Hash256 ExpectedAnchorContextHash { get; }

    public Hash256 OldArtifactHash { get; }

    public IReadOnlyList<AnchorTarget> OldAnchorTargets { get; }

    public Hash256 NewArtifactHash { get; }

    public IReadOnlyList<AnchorTarget> NewAnchorTargets { get; }

    public IReadOnlyList<AnchorKey> AnchorActivationOrder { get; }

    public IReadOnlyList<AnchorKey> AnchorRollbackOrder { get; }

    public IReadOnlyList<Hash256> TransitionStateHashes { get; }

    public TimeSpan RollbackTtl { get; }

    public IReadOnlyList<DeploymentProbe> Probes { get; }

    public static DeviceDeploymentPlan Create(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedGuardContextHash,
        Hash256 expectedAnchorContextHash,
        Hash256 oldArtifactHash,
        IReadOnlyList<AnchorTarget> oldAnchorTargets,
        Hash256 newArtifactHash,
        IReadOnlyList<AnchorTarget> newAnchorTargets,
        IReadOnlyList<AnchorKey> anchorActivationOrder,
        IReadOnlyList<AnchorKey> anchorRollbackOrder,
        IReadOnlyList<Hash256> transitionStateHashes,
        TimeSpan? rollbackTtl = null,
        IReadOnlyList<DeploymentProbe>? probes = null)
        => Validate(
            deviceId,
            expectedRouterOsVersion,
            expectedCapabilityHash,
            expectedConfigurationHash,
            expectedCompatibilityHash,
            expectedGuardContextHash,
            expectedAnchorContextHash,
            oldArtifactHash,
            oldAnchorTargets,
            newArtifactHash,
            newAnchorTargets,
            anchorActivationOrder,
            anchorRollbackOrder,
            transitionStateHashes,
            rollbackTtl ?? DeploymentCodes.DefaultRollbackTtl,
            probes ?? []);

    public static DeviceDeploymentPlan Reconstitute(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedGuardContextHash,
        Hash256 expectedAnchorContextHash,
        Hash256 oldArtifactHash,
        IReadOnlyList<AnchorTarget> oldAnchorTargets,
        Hash256 newArtifactHash,
        IReadOnlyList<AnchorTarget> newAnchorTargets,
        IReadOnlyList<AnchorKey> anchorActivationOrder,
        IReadOnlyList<AnchorKey> anchorRollbackOrder,
        IReadOnlyList<Hash256> transitionStateHashes,
        TimeSpan rollbackTtl,
        IReadOnlyList<DeploymentProbe> probes)
        => Validate(
            deviceId,
            expectedRouterOsVersion,
            expectedCapabilityHash,
            expectedConfigurationHash,
            expectedCompatibilityHash,
            expectedGuardContextHash,
            expectedAnchorContextHash,
            oldArtifactHash,
            oldAnchorTargets,
            newArtifactHash,
            newAnchorTargets,
            anchorActivationOrder,
            anchorRollbackOrder,
            transitionStateHashes,
            rollbackTtl,
            probes);

    private static DeviceDeploymentPlan Validate(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        Hash256 expectedCapabilityHash,
        Hash256 expectedConfigurationHash,
        Hash256 expectedCompatibilityHash,
        Hash256 expectedGuardContextHash,
        Hash256 expectedAnchorContextHash,
        Hash256 oldArtifactHash,
        IReadOnlyList<AnchorTarget> oldAnchorTargets,
        Hash256 newArtifactHash,
        IReadOnlyList<AnchorTarget> newAnchorTargets,
        IReadOnlyList<AnchorKey> anchorActivationOrder,
        IReadOnlyList<AnchorKey> anchorRollbackOrder,
        IReadOnlyList<Hash256> transitionStateHashes,
        TimeSpan rollbackTtl,
        IReadOnlyList<DeploymentProbe> probes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRouterOsVersion);
        ArgumentNullException.ThrowIfNull(expectedCapabilityHash);
        ArgumentNullException.ThrowIfNull(expectedConfigurationHash);
        ArgumentNullException.ThrowIfNull(expectedCompatibilityHash);
        ArgumentNullException.ThrowIfNull(expectedGuardContextHash);
        ArgumentNullException.ThrowIfNull(expectedAnchorContextHash);
        ArgumentNullException.ThrowIfNull(oldArtifactHash);
        ArgumentNullException.ThrowIfNull(oldAnchorTargets);
        ArgumentNullException.ThrowIfNull(newArtifactHash);
        ArgumentNullException.ThrowIfNull(newAnchorTargets);
        ArgumentNullException.ThrowIfNull(anchorActivationOrder);
        ArgumentNullException.ThrowIfNull(anchorRollbackOrder);
        ArgumentNullException.ThrowIfNull(transitionStateHashes);
        ArgumentNullException.ThrowIfNull(probes);
        if (rollbackTtl < DeploymentCodes.MinRollbackTtl || rollbackTtl > DeploymentCodes.MaxRollbackTtl)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.RollbackTtlOutOfRange}: TTL must be 60–600 seconds.");
        }

        if (oldAnchorTargets.Count == 0 || newAnchorTargets.Count == 0)
        {
            throw new DomainInvariantException("old/new anchor targets must be non-empty.");
        }

        if (anchorActivationOrder.Count == 0
            || anchorActivationOrder.Count != anchorRollbackOrder.Count
            || !anchorRollbackOrder.SequenceEqual(anchorActivationOrder.Reverse()))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ActivationOrderInvalid}: rollback order must reverse activation order.");
        }

        if (!DeploymentAnchorOrder.IsManagementCriticalLast(anchorActivationOrder))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ActivationOrderInvalid}: management-critical anchors must be last.");
        }

        HashSet<string> activation = anchorActivationOrder.Select(static k => k.Marker).ToHashSet(StringComparer.Ordinal);
        if (activation.Count != anchorActivationOrder.Count)
        {
            throw new DomainInvariantException("anchor activation order must be unique.");
        }

        EnsureTargetsCover(oldAnchorTargets, activation, "old");
        EnsureTargetsCover(newAnchorTargets, activation, "new");
        if (transitionStateHashes.Count != anchorActivationOrder.Count + 1)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.TransitionStateUnsafe}: transition_state_hashes must cover states 0..N.");
        }

        return new DeviceDeploymentPlan(
            deviceId,
            expectedRouterOsVersion.Trim(),
            expectedCapabilityHash,
            expectedConfigurationHash,
            expectedCompatibilityHash,
            expectedGuardContextHash,
            expectedAnchorContextHash,
            oldArtifactHash,
            oldAnchorTargets.ToArray(),
            newArtifactHash,
            newAnchorTargets.ToArray(),
            anchorActivationOrder.ToArray(),
            anchorRollbackOrder.ToArray(),
            transitionStateHashes.ToArray(),
            rollbackTtl,
            probes.ToArray());
    }

    private static void EnsureTargetsCover(
        IReadOnlyList<AnchorTarget> targets,
        HashSet<string> markers,
        string label)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (AnchorTarget target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (!markers.Contains(target.Key.Marker) || !seen.Add(target.Key.Marker))
            {
                throw new DomainInvariantException($"{label} anchor targets must cover each activation key exactly once.");
            }
        }

        if (seen.Count != markers.Count)
        {
            throw new DomainInvariantException($"{label} anchor targets must cover the full activation set.");
        }
    }
}
