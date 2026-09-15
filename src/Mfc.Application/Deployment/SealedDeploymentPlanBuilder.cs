using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Workflow;

namespace Mfc.Application.Deployment;

/// <summary>
/// Builds a <see cref="DeviceDeploymentPlan"/> from sealed filter-artifact store bodies + hash state (AUDIT-GUI-01).
/// Does not invent artifact hashes, probe destinations, or packet-path interfaces.
/// </summary>
public static class SealedDeploymentPlanBuilder
{
    /// <summary>
    /// Constructs one device plan. Old anchors come from the prior sealed body when present;
    /// otherwise bootstrap roots. Transition evidence uses <see cref="TransitionStateValidator.AllSafeEvidence"/>
    /// after compile/approval (sealed plans with proven analysis).
    /// </summary>
    public static DeviceDeploymentPlan Build(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        StoredFilterArtifact newMeta,
        RouterOsFilterArtifactReader.ParsedBody newBody,
        DeviceHashState? hashState,
        RouterOsFilterArtifactReader.ParsedBody? oldBody,
        ConfigurationHash? configurationHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRouterOsVersion);
        ArgumentNullException.ThrowIfNull(newMeta);
        ArgumentNullException.ThrowIfNull(newBody);

        if (!newMeta.DeviceId.Equals(deviceId))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: sealed artifact device_id does not match plan device.");
        }

        List<AnchorTarget> newTargets = ToAnchorTargets(newBody.Anchors, "new");
        if (newTargets.Count == 0)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.AnchorInvalid}: sealed filter artifact has no anchor targets.");
        }

        Hash256 oldArtifactHash = hashState?.LastCommittedArtifactHash ?? BootstrapArtifact.Hash;
        List<AnchorTarget> oldTargets = oldBody is null
            ? BootstrapTargets(newTargets.Select(static t => t.Key).ToArray())
            : ToAnchorTargets(oldBody.Anchors, "old");

        IReadOnlyList<AnchorKey> activation = PlanTransitionStatesUseCase.PlanActivationOrder(
            newTargets.Select(static t => t.Key));
        TransitionStateValidationResult transitions = PlanTransitionStatesUseCase.ValidateTransitions(
            activation,
            oldTargets,
            newTargets,
            TransitionStateValidator.AllSafeEvidence(activation.Count));
        if (transitions.HasBlockers)
        {
            throw new DomainInvariantException(
                string.Join(';', transitions.Findings.Select(static f => $"{f.Code}:{f.Message}")));
        }

        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        Hash256 cfg = configurationHash is { } c ? c.Value : empty;
        Hash256 capability = newMeta.CapabilityHash;
        Hash256 anchorGuard = HashOrdered([cfg, capability]);

        return DeviceDeploymentPlan.Create(
            deviceId,
            expectedRouterOsVersion.Trim(),
            capability,
            cfg,
            empty,
            anchorGuard,
            anchorGuard,
            oldArtifactHash,
            oldTargets,
            newMeta.ResourceHash,
            newTargets,
            activation,
            activation.Reverse().ToArray(),
            transitions.TransitionStateHashes,
            DeploymentCodes.DefaultRollbackTtl,
            probes: []);
    }

    private static List<AnchorTarget> ToAnchorTargets(
        IReadOnlyList<AnchorTargetArtifact> anchors,
        string label)
    {
        List<AnchorTarget> targets = new(anchors.Count);
        foreach (AnchorTargetArtifact anchor in anchors)
        {
            if (!AnchorKey.TryParse(anchor.ExpectedAnchorComment, out AnchorKey? key) || key is null)
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.AnchorInvalid}: {label} sealed anchor comment is not a permanent marker.");
            }

            targets.Add(new AnchorTarget(key, anchor.DesiredJumpTarget));
        }

        return targets;
    }

    private static List<AnchorTarget> BootstrapTargets(AnchorKey[] keys)
    {
        List<AnchorTarget> targets = new(keys.Length);
        foreach (AnchorKey key in keys)
        {
            targets.Add(new AnchorTarget(key, BootstrapArtifact.RootChainName(key.Family, key.Chain)));
        }

        return targets;
    }

    private static Hash256 HashOrdered(IReadOnlyList<Hash256> digests)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes("mfc.deployment.expected_ctx.v1"));
        hasher.AppendData([(byte)0]);
        foreach (Hash256 digest in digests)
        {
            hasher.AppendData(digest.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }
}
