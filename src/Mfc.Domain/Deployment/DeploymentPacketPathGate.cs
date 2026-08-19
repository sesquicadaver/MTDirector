using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Blocks Node deployment when managed FORWARD packet path is unproven (next-1 / N1-06).
/// Does not disable L2/L3 hardware offload and does not mutate RouterOS.
/// </summary>
public static class DeploymentPacketPathGate
{
    public static bool RequiresManagedForwardProof(NodeKind kind)
        => kind is NodeKind.Router or NodeKind.Vrrp;

    public static string? DescribeBlocker(NodeKind kind, IReadOnlyList<PacketPathPairFact> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (!RequiresManagedForwardProof(kind))
        {
            return null;
        }

        if (pairs.Count == 0)
        {
            return PacketPathAnalysisCodes.NotProven;
        }

        PacketPathAnalysisResult result = PacketPathAnalysis.Analyze(pairs);
        return result.Findings
            .FirstOrDefault(static f => f.Severity == PacketPathAnalysisCodes.SeverityBlocker)
            ?.Code;
    }

    public static void EnsureCleared(NodeKind kind, IReadOnlyList<PacketPathPairFact> pairs)
    {
        string? code = DescribeBlocker(kind, pairs);
        if (code is not null)
        {
            throw new DomainInvariantException(
                $"{code}: packet path through the IP firewall is not proven; deployment is blocked.");
        }
    }

    /// <summary>
    /// PRECHECKING → BLOCKED when packet-path blockers are present. Does not enter STAGING.
    /// </summary>
    public static bool TryAllowStaging(
        DeploymentOperation operation,
        NodeKind kind,
        IReadOnlyList<PacketPathPairFact> pairs,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(operation);
        string? blocker = DescribeBlocker(kind, pairs);
        if (blocker is null)
        {
            return true;
        }

        if (operation.State == DeploymentOperationState.Created)
        {
            operation.EnsureTransition(DeploymentOperationState.Prechecking, nowUtc);
        }

        operation.EnsureTransition(DeploymentOperationState.Blocked, nowUtc, blocker);
        return false;
    }
}
