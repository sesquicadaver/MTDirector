using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>
/// Canonical-record packet-path deploy precheck (N1-06). Does not re-classify offload or write RouterOS.
/// </summary>
public static class DeploymentPacketPathPrecheck
{
    public static string? DescribeBlocker(NodeKind kind, IReadOnlyList<CanonicalRecord> pairRecords)
    {
        ArgumentNullException.ThrowIfNull(pairRecords);
        return DeploymentPacketPathGate.DescribeBlocker(kind, PacketPathContextMapper.FromCanonicalPairs(pairRecords));
    }

    public static void EnsureCleared(NodeKind kind, IReadOnlyList<CanonicalRecord> pairRecords)
        => DeploymentPacketPathGate.EnsureCleared(kind, PacketPathContextMapper.FromCanonicalPairs(pairRecords));
}
