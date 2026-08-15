using Mfc.Domain.Canonicalization;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>
/// Maps canonical packet-path pair records onto Domain analysis (N1-04).
/// Does not call RouterOS and does not re-classify hardware offload.
/// </summary>
public static class PacketPathContextMapper
{
    public static PacketPathAnalysisResult Analyze(IReadOnlyList<CanonicalRecord> pairRecords)
    {
        ArgumentNullException.ThrowIfNull(pairRecords);
        return PacketPathAnalysis.Analyze(FromCanonicalPairs(pairRecords));
    }

    public static IReadOnlyList<PacketPathPairFact> FromCanonicalPairs(IReadOnlyList<CanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        List<PacketPathPairFact> pairs = new(records.Count);
        foreach (CanonicalRecord record in records)
        {
            IReadOnlyDictionary<string, string> properties = record.Properties;
            pairs.Add(PacketPathPairFact.Create(
                Get(properties, "ingress") ?? string.Empty,
                Get(properties, "egress") ?? string.Empty,
                PacketPathAnalysis.ParseClassName(Get(properties, "class") ?? "INDETERMINATE"),
                bridge: Get(properties, "bridge"),
                vlanId: Get(properties, "vlan-id")));
        }

        return pairs;
    }

    private static string? Get(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out string? value) ? value : null;
}
