using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Content-addresses <see cref="DeploymentPlan"/> (Safe Deployment Spec §9–§10).
/// Includes normative preconditions; excludes live VRRP role and active WAN.
/// </summary>
public static class DeploymentPlanHasher
{
    public static Hash256 Compute(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Hash(
            plan.NodeId,
            plan.LogicalPolicyHash,
            plan.AnalysisBundleHash,
            plan.TopologyProjectionHash,
            plan.DevicePlans,
            plan.ActivationOrder,
            plan.RollbackOrder,
            plan.CreatedBy,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc);
    }

    public static Hash256 Hash(
        NodeId nodeId,
        Hash256 logicalPolicyHash,
        Hash256 analysisBundleHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceDeploymentPlan> devicePlans,
        IReadOnlyList<DeviceId> activationOrder,
        IReadOnlyList<DeviceId> rollbackOrder,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(logicalPolicyHash);
        ArgumentNullException.ThrowIfNull(analysisBundleHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(devicePlans);
        ArgumentNullException.ThrowIfNull(activationOrder);
        ArgumentNullException.ThrowIfNull(rollbackOrder);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, DeploymentCodes.PlanHashPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, DeploymentCodes.SchemaVersion);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, DeploymentCodes.CompilerVersionSlot);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, nodeId.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        hasher.AppendData(logicalPolicyHash.Bytes);
        hasher.AppendData(analysisBundleHash.Bytes);
        hasher.AppendData(topologyProjectionHash.Bytes);
        AppendUtf8(hasher, createdBy.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        AppendInt64Be(hasher, createdAtUtc.ToUniversalTime().UtcTicks);
        AppendInt64Be(hasher, expiresAtUtc.ToUniversalTime().UtcTicks);
        AppendUInt32Be(hasher, (uint)activationOrder.Count);
        foreach (DeviceId id in activationOrder)
        {
            AppendUtf8(hasher, id.Value.ToString("D"));
            hasher.AppendData([(byte)0]);
        }

        AppendUInt32Be(hasher, (uint)rollbackOrder.Count);
        foreach (DeviceId id in rollbackOrder)
        {
            AppendUtf8(hasher, id.Value.ToString("D"));
            hasher.AppendData([(byte)0]);
        }

        AppendUInt32Be(hasher, (uint)devicePlans.Count);
        foreach (DeviceDeploymentPlan plan in devicePlans.OrderBy(static p => p.DeviceId.Value))
        {
            AppendDevice(hasher, plan);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void AppendDevice(IncrementalHash hasher, DeviceDeploymentPlan plan)
    {
        AppendUtf8(hasher, plan.DeviceId.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, plan.ExpectedRouterOsVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(plan.ExpectedCapabilityHash.Bytes);
        hasher.AppendData(plan.ExpectedConfigurationHash.Bytes);
        hasher.AppendData(plan.ExpectedCompatibilityHash.Bytes);
        hasher.AppendData(plan.ExpectedGuardContextHash.Bytes);
        hasher.AppendData(plan.ExpectedAnchorContextHash.Bytes);
        hasher.AppendData(plan.OldArtifactHash.Bytes);
        hasher.AppendData(plan.NewArtifactHash.Bytes);
        AppendTargets(hasher, plan.OldAnchorTargets);
        AppendTargets(hasher, plan.NewAnchorTargets);
        AppendUInt32Be(hasher, (uint)plan.AnchorActivationOrder.Count);
        foreach (AnchorKey key in plan.AnchorActivationOrder)
        {
            AppendUtf8(hasher, key.Marker);
            hasher.AppendData([(byte)0]);
        }

        AppendUInt32Be(hasher, (uint)plan.AnchorRollbackOrder.Count);
        foreach (AnchorKey key in plan.AnchorRollbackOrder)
        {
            AppendUtf8(hasher, key.Marker);
            hasher.AppendData([(byte)0]);
        }

        AppendUInt32Be(hasher, (uint)plan.TransitionStateHashes.Count);
        foreach (Hash256 hash in plan.TransitionStateHashes)
        {
            hasher.AppendData(hash.Bytes);
        }

        AppendUInt32Be(hasher, (uint)plan.RollbackTtl.TotalSeconds);
        AppendUInt32Be(hasher, (uint)plan.Probes.Count);
        foreach (DeploymentProbe probe in plan.Probes)
        {
            hasher.AppendData([(byte)probe.Kind]);
            AppendUtf8(hasher, probe.Destination);
            hasher.AppendData([(byte)0]);
            AppendUInt32Be(hasher, (uint)probe.TimeoutMilliseconds);
        }
    }

    private static void AppendTargets(IncrementalHash hasher, IReadOnlyList<AnchorTarget> targets)
    {
        AppendUInt32Be(hasher, (uint)targets.Count);
        foreach (AnchorTarget target in targets.OrderBy(static t => t.Key.Marker, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, target.Key.Marker);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, target.JumpTarget);
            hasher.AppendData([(byte)0]);
        }
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendUInt32Be(IncrementalHash hasher, uint value)
    {
        Span<byte> slot = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(slot, value);
        hasher.AppendData(slot);
    }

    private static void AppendInt64Be(IncrementalHash hasher, long value)
    {
        Span<byte> slot = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(slot, value);
        hasher.AppendData(slot);
    }
}
