using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps N1-03 classification onto Domain packet-path analysis blockers (N1-04).
/// Does not disable hardware offload and does not re-run switch-chip classification.
/// </summary>
public static class PacketPathBlockerMapper
{
    public static PacketPathAnalysisResult Analyze(PacketPathClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        return PacketPathAnalysis.Analyze(FromClassification(classification));
    }

    public static IReadOnlyList<PacketPathPairFact> FromClassification(PacketPathClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        List<PacketPathPairFact> pairs = new(classification.Pairs.Count);
        foreach (PacketPathPairClassification pair in classification.Pairs)
        {
            pairs.Add(PacketPathPairFact.Create(
                pair.IngressInterface,
                pair.EgressInterface,
                MapClass(pair.PathClass),
                bridge: pair.Bridge,
                vlanId: pair.VlanId));
        }

        return pairs;
    }

    private static PacketPathKind MapClass(PacketPathClass pathClass)
        => pathClass switch
        {
            PacketPathClass.CpuFirewallPath => PacketPathKind.CpuFirewallPath,
            PacketPathClass.HardwareOffloadedPath => PacketPathKind.HardwareOffloadedPath,
            PacketPathClass.MixedPath => PacketPathKind.MixedPath,
            PacketPathClass.Indeterminate => PacketPathKind.Indeterminate,
            _ => PacketPathKind.Indeterminate,
        };
}
