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
/// Safety evidence is Controller-proven from sealed bodies (AUDIT-EVID-01); never <c>AllSafeEvidence</c>.
/// </summary>
public static class SealedDeploymentPlanBuilder
{
    /// <summary>
    /// Constructs one device plan. Old anchors come from the prior sealed body when present;
    /// bootstrap roots only when <see cref="BootstrapArtifact.Hash"/> is the committed base.
    /// Missing configuration, probes, capability, or committed-old body without bytes blocks sealing.
    /// </summary>
    public static DeviceDeploymentPlan Build(
        DeviceId deviceId,
        string expectedRouterOsVersion,
        StoredFilterArtifact newMeta,
        RouterOsFilterArtifactReader.ParsedBody newBody,
        DeviceHashState? hashState,
        RouterOsFilterArtifactReader.ParsedBody? oldBody,
        ConfigurationHash configurationHash,
        IReadOnlyList<DeploymentProbe> probes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRouterOsVersion);
        ArgumentNullException.ThrowIfNull(newMeta);
        ArgumentNullException.ThrowIfNull(newBody);
        ArgumentNullException.ThrowIfNull(probes);

        if (!newMeta.DeviceId.Equals(deviceId))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: sealed artifact device_id does not match plan device.");
        }

        if (IsEmpty(newMeta.CapabilityHash))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.SealedEvidenceMissing}: sealed artifact capability hash is required.");
        }

        if (IsEmpty(configurationHash.Value))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.SealedEvidenceMissing}: configuration hash from last capture is required.");
        }

        if (probes.Count == 0 || probes.All(static p => p.Kind != DeploymentProbeKind.ApiSsl))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.SealedEvidenceMissing}: sealed plan requires at least one API_SSL probe.");
        }

        List<AnchorTarget> newTargets = ToAnchorTargets(newBody.Anchors, "new");
        if (newTargets.Count == 0)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.AnchorInvalid}: sealed filter artifact has no anchor targets.");
        }

        Hash256 oldArtifactHash = hashState?.LastCommittedArtifactHash ?? BootstrapArtifact.Hash;
        bool oldIsBootstrap = oldArtifactHash.Equals(BootstrapArtifact.Hash);
        if (!oldIsBootstrap && oldBody is null)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.SealedEvidenceMissing}: committed old artifact body is missing; bootstrap fallback forbidden.");
        }

        List<AnchorTarget> oldTargets = oldIsBootstrap
            ? BootstrapTargets(newTargets.Select(static t => t.Key).ToArray())
            : ToAnchorTargets(oldBody!.Anchors, "old");

        IReadOnlyList<AnchorKey> activation = PlanTransitionStatesUseCase.PlanActivationOrder(
            newTargets.Select(static t => t.Key));
        IReadOnlyList<TransitionStateEvidence> evidence = SealedTransitionEvidence.Prove(
            activation,
            oldTargets,
            newTargets,
            oldBody,
            newBody,
            oldIsBootstrap);
        TransitionStateValidationResult transitions = PlanTransitionStatesUseCase.ValidateTransitions(
            activation,
            oldTargets,
            newTargets,
            evidence);
        if (transitions.HasBlockers)
        {
            throw new DomainInvariantException(
                string.Join(';', transitions.Findings.Select(static f => $"{f.Code}:{f.Message}")));
        }

        Hash256 capability = newMeta.CapabilityHash;
        Hash256 cfg = configurationHash.Value;
        List<Hash256> compatibilityParts =
        [
            Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(expectedRouterOsVersion.Trim()))),
            capability,
        ];
        Hash256 compatibility = HashOrdered("mfc.deployment.expected_compatibility.v1", compatibilityParts);
        Hash256 guardContext = HashOrdered("mfc.deployment.expected_guard_ctx.v1", [cfg, capability]);
        List<Hash256> anchorParts = [oldArtifactHash, newMeta.ResourceHash];
        foreach (AnchorKey key in activation)
        {
            anchorParts.Add(Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(key.Marker))));
        }

        Hash256 anchorContext = HashOrdered("mfc.deployment.expected_anchor_ctx.v1", anchorParts);

        return DeviceDeploymentPlan.Create(
            deviceId,
            expectedRouterOsVersion.Trim(),
            capability,
            cfg,
            compatibility,
            guardContext,
            anchorContext,
            oldArtifactHash,
            oldTargets,
            newMeta.ResourceHash,
            newTargets,
            activation,
            activation.Reverse().ToArray(),
            transitions.TransitionStateHashes,
            DeploymentCodes.DefaultRollbackTtl,
            probes);
    }

    /// <summary>Builds the required API_SSL probe from a literal management IP (AUDIT-EVID-01).</summary>
    public static DeploymentProbe RequireApiSslProbe(ManagementEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Host.Address is null)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.SealedEvidenceMissing}: management host must be a literal IP for API_SSL probe.");
        }

        return new DeploymentProbe(
            DeploymentProbeKind.ApiSsl,
            endpoint.Host.Value,
            timeoutMilliseconds: 1000);
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

    private static bool IsEmpty(Hash256 hash)
        => hash.Bytes.ToArray().All(static b => b == 0);

    private static Hash256 HashOrdered(string label, IReadOnlyList<Hash256> digests)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes(label));
        hasher.AppendData([(byte)0]);
        foreach (Hash256 digest in digests)
        {
            hasher.AppendData(digest.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }
}
