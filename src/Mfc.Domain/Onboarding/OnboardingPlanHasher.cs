using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Content-addresses <see cref="OnboardingPlan"/> (Onboarding Spec §25–§26).
/// Excludes current VRRP role and active WAN.
/// </summary>
public static class OnboardingPlanHasher
{
    public static Hash256 Compute(OnboardingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Compute(
            plan.NodeId,
            plan.NodeMembershipHash,
            plan.TopologyProjectionHash,
            plan.DevicePlans,
            plan.CreatedBy,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc);
    }

    /// <summary>Alias of <see cref="Compute(OnboardingPlan)"/>.</summary>
    public static Hash256 Hash(OnboardingPlan plan) => Compute(plan);

    public static Hash256 Compute(
        NodeId nodeId,
        Hash256 nodeMembershipHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceOnboardingPlan> devicePlans,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
        => Hash(nodeId, nodeMembershipHash, topologyProjectionHash, devicePlans, createdBy, createdAtUtc, expiresAtUtc);

    public static Hash256 Hash(
        NodeId nodeId,
        Hash256 nodeMembershipHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceOnboardingPlan> devicePlans,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(nodeMembershipHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(devicePlans);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, OnboardingCodes.PlanHashPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, nodeId.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        hasher.AppendData(nodeMembershipHash.Bytes);
        hasher.AppendData(topologyProjectionHash.Bytes);
        AppendUtf8(hasher, createdBy.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        AppendInt64Be(hasher, createdAtUtc.ToUniversalTime().UtcTicks);
        AppendInt64Be(hasher, expiresAtUtc.ToUniversalTime().UtcTicks);
        AppendUInt32Be(hasher, (uint)devicePlans.Count);
        foreach (DeviceOnboardingPlan plan in devicePlans.OrderBy(static p => p.DeviceId.Value))
        {
            ArgumentNullException.ThrowIfNull(plan);
            AppendDevice(hasher, plan);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void AppendDevice(IncrementalHash hasher, DeviceOnboardingPlan plan)
    {
        AppendUtf8(hasher, plan.DeviceId.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, plan.ExpectedRouterOsVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(plan.ExpectedCapabilityHash.Bytes);
        hasher.AppendData(plan.ExpectedConfigurationHash.Bytes);
        hasher.AppendData(plan.ExpectedCompatibilityHash.Bytes);
        hasher.AppendData(plan.ExpectedApiServiceHash.Bytes);
        hasher.AppendData(plan.ExpectedReadAccountHash.Bytes);
        hasher.AppendData(plan.ExpectedDeploymentAccountHash.Bytes);
        hasher.AppendData(plan.ExpectedDeviceModeHash.Bytes);
        hasher.AppendData(plan.ExpectedGuardHash.Bytes);
        AppendUInt32Be(hasher, (uint)plan.RequiredAnchorSet.Count);
        foreach (AnchorKey key in plan.RequiredAnchorSet)
        {
            AppendUtf8(hasher, key.Marker);
            hasher.AppendData([(byte)0]);
        }

        AppendUInt32Be(hasher, (uint)plan.AnchorPlacements.Count);
        foreach (AnchorPlacement placement in plan.AnchorPlacements)
        {
            hasher.AppendData([(byte)placement.Family]);
            hasher.AppendData([(byte)placement.Chain]);
            hasher.AppendData([(byte)placement.Mode]);
            AppendOptionalHash(hasher, placement.ReferenceRuleFingerprint);
            AppendOptionalUInt32(hasher, placement.ReferenceOccurrenceRank);
            AppendOptionalHash(hasher, placement.ExpectedPredecessorFingerprint);
            AppendOptionalHash(hasher, placement.ExpectedSuccessorFingerprint);
            AppendUInt32Be(hasher, placement.ExpectedAnchorOrdinal);
        }

        hasher.AppendData(plan.BootstrapArtifactHash.Bytes);
        AppendUInt32Be(hasher, (uint)plan.WatchdogTtl.TotalSeconds);
    }

    private static void AppendOptionalHash(IncrementalHash hasher, Hash256? hash)
    {
        if (hash is null)
        {
            hasher.AppendData([(byte)0]);
            return;
        }

        hasher.AppendData([(byte)1]);
        hasher.AppendData(hash.Bytes);
    }

    private static void AppendOptionalUInt32(IncrementalHash hasher, uint? value)
    {
        if (value is null)
        {
            hasher.AppendData([(byte)0]);
            return;
        }

        hasher.AppendData([(byte)1]);
        AppendUInt32Be(hasher, value.Value);
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
