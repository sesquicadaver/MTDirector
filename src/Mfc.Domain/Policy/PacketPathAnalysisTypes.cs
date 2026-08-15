using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>next-1 packet path class consumed by analysis (N1-04). Independent of RouterOs types.</summary>
public enum PacketPathKind : byte
{
    CpuFirewallPath = 0,
    HardwareOffloadedPath = 1,
    MixedPath = 2,
    Indeterminate = 3,
}

/// <summary>
/// One classified ingress/egress pair. Identity is interface names, not a managed UUID.
/// Controller never infers class from interface count alone.
/// </summary>
public sealed class PacketPathPairFact
{
    public required string IngressInterface { get; init; }

    public required string EgressInterface { get; init; }

    public string? Bridge { get; init; }

    public string? VlanId { get; init; }

    public required PacketPathKind PathClass { get; init; }

    public static PacketPathPairFact Create(
        string ingressInterface,
        string egressInterface,
        PacketPathKind pathClass,
        string? bridge = null,
        string? vlanId = null)
    {
        if (string.IsNullOrWhiteSpace(ingressInterface))
        {
            throw new DomainInvariantException("Packet-path ingress interface is required.");
        }

        if (string.IsNullOrWhiteSpace(egressInterface))
        {
            throw new DomainInvariantException("Packet-path egress interface is required.");
        }

        if (!Enum.IsDefined(pathClass))
        {
            throw new DomainInvariantException($"Unsupported packet-path class '{pathClass}'.");
        }

        return new PacketPathPairFact
        {
            IngressInterface = ingressInterface.Trim(),
            EgressInterface = egressInterface.Trim(),
            Bridge = string.IsNullOrWhiteSpace(bridge) ? null : bridge.Trim(),
            VlanId = string.IsNullOrWhiteSpace(vlanId) ? null : vlanId.Trim(),
            PathClass = pathClass,
        };
    }
}

/// <summary>One packet-path analysis finding. Subject is ingress→egress, not a UUID.</summary>
public sealed class PacketPathFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public required string? IngressInterface { get; init; }

    public required string? EgressInterface { get; init; }

    public string? Bridge { get; init; }

    public string? VlanId { get; init; }
}

/// <summary>Outcome of <see cref="PacketPathAnalysis.Analyze"/>.</summary>
public sealed class PacketPathAnalysisResult
{
    public required IReadOnlyList<PacketPathFinding> Findings { get; init; }

    /// <summary>SHA-256 of ordered pair identity + class (observation slot; next-1).</summary>
    public required Hash256 PacketPathContextHash { get; init; }

    /// <summary>
    /// True when any pair is HARDWARE_OFFLOADED_PATH or INDETERMINATE
    /// (next-1 managed FORWARD gating). MIXED does not set this flag.
    /// </summary>
    public required bool BlocksManagedForwardPolicy { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == PacketPathAnalysisCodes.SeverityBlocker);
}
