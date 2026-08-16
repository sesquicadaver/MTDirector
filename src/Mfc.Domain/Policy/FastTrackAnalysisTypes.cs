using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Topology and live-filter facts that gate FASTTRACK_ACCEPT (Policy Model §52.1–§52.3).
/// Observations such as VRRP role are not stored here.
/// </summary>
public sealed class FastTrackTopologyContext
{
    public required DeclaredUplinkMode UplinkMode { get; init; }

    public required bool HasPcc { get; init; }

    public required bool HasRoutingMarks { get; init; }

    public required bool HasNonMainRoutingTables { get; init; }

    public required bool HasUnknownMangle { get; init; }

    public required bool HasVrf { get; init; }

    public required bool HasPreAnchorUnmanagedFastTrack { get; init; }

    public required bool ConnectionTrackingPresent { get; init; }

    public bool HasHotSpot { get; init; }

    public bool HasGlobalQueueTree { get; init; }

    public bool HasPacketMarksRequiredAfterFastTrack { get; init; }

    /// <summary>SINGLE/ONE uplink, main table, no PCC/marks/VRF — the only topology that can pass after rule checks.</summary>
    public static FastTrackTopologyContext SafeSingleWan { get; } = Create(DeclaredUplinkMode.One);

    public static FastTrackTopologyContext Create(
        DeclaredUplinkMode uplinkMode = DeclaredUplinkMode.One,
        bool hasPcc = false,
        bool hasRoutingMarks = false,
        bool hasNonMainRoutingTables = false,
        bool hasUnknownMangle = false,
        bool hasVrf = false,
        bool hasPreAnchorUnmanagedFastTrack = false,
        bool connectionTrackingPresent = true,
        bool hasHotSpot = false,
        bool hasGlobalQueueTree = false,
        bool hasPacketMarksRequiredAfterFastTrack = false)
        => new()
        {
            UplinkMode = uplinkMode,
            HasPcc = hasPcc,
            HasRoutingMarks = hasRoutingMarks,
            HasNonMainRoutingTables = hasNonMainRoutingTables,
            HasUnknownMangle = hasUnknownMangle,
            HasVrf = hasVrf,
            HasPreAnchorUnmanagedFastTrack = hasPreAnchorUnmanagedFastTrack,
            ConnectionTrackingPresent = connectionTrackingPresent,
            HasHotSpot = hasHotSpot,
            HasGlobalQueueTree = hasGlobalQueueTree,
            HasPacketMarksRequiredAfterFastTrack = hasPacketMarksRequiredAfterFastTrack,
        };

    /// <summary>Derives FastTrack topology flags from M2-14 facts. VRF / pre-anchor / HotSpot stay caller-supplied.</summary>
    public static FastTrackTopologyContext From(
        TopologyDependencyFacts facts,
        bool hasVrf = false,
        bool hasPreAnchorUnmanagedFastTrack = false,
        bool connectionTrackingPresent = true,
        bool hasHotSpot = false,
        bool hasGlobalQueueTree = false)
    {
        ArgumentNullException.ThrowIfNull(facts);
        bool hasPcc = facts.MangleRules.Any(static r =>
            !r.Disabled
            && (!string.IsNullOrWhiteSpace(r.PerConnectionClassifier)
                || r.UnsupportedMatchers.Contains("per-connection-classifier", StringComparer.Ordinal)));
        bool hasRoutingMarks = facts.MangleRules.Any(static r =>
                                 !r.Disabled
                                 && (!string.IsNullOrWhiteSpace(r.RoutingMark)
                                     || !string.IsNullOrWhiteSpace(r.NewRoutingMark)))
                             || facts.RoutingRules.Any(static r =>
                                 !r.Disabled && !string.IsNullOrWhiteSpace(r.RoutingMark));
        bool hasNonMain = facts.RoutingTables.Any(static t =>
                              !t.Disabled && !string.Equals(t.Name, "main", StringComparison.OrdinalIgnoreCase))
                          || facts.RoutingRules.Any(static r =>
                              !r.Disabled
                              && !string.IsNullOrWhiteSpace(r.Table)
                              && !string.Equals(r.Table, "main", StringComparison.OrdinalIgnoreCase));
        bool unknownMangle = facts.MangleRules.Any(static r =>
            !r.Disabled
            && r.UnsupportedMatchers.Any(static m =>
                !string.Equals(m, "per-connection-classifier", StringComparison.Ordinal)));
        bool packetMarks = facts.MangleRules.Any(static r =>
            !r.Disabled
            && (!string.IsNullOrWhiteSpace(r.PacketMark) || !string.IsNullOrWhiteSpace(r.NewPacketMark)));
        return Create(
            facts.UplinkMode,
            hasPcc,
            hasRoutingMarks,
            hasNonMain,
            unknownMangle,
            hasVrf,
            hasPreAnchorUnmanagedFastTrack,
            connectionTrackingPresent,
            hasHotSpot,
            hasGlobalQueueTree,
            packetMarks);
    }
}

/// <summary>One FastTrack analysis finding. Subject is rule id or topology key — not a UUID of a live capture.</summary>
public sealed class FastTrackFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }

    public string Risk { get; init; } = FastTrackAnalysisCodes.RiskHigh;
}

/// <summary>Outcome of <see cref="FastTrackAnalysis.Analyze"/>.</summary>
public sealed class FastTrackAnalysisResult
{
    public required IReadOnlyList<FastTrackFinding> Findings { get; init; }

    public required Hash256 FastTrackContextHash { get; init; }

    /// <summary>True when any FASTTRACK_ACCEPT rule exists — compiler must emit the adjacent ACCEPT pair.</summary>
    public required bool RequiresAcceptFallback { get; init; }

    /// <summary>HIGH whenever FastTrack is present; null when the document has no FastTrack rules.</summary>
    public string? RiskFloor { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == FastTrackAnalysisCodes.SeverityBlocker);

    /// <summary>True when FastTrack is present, fallback is required, and no safety BLOCKER fired.</summary>
    public bool AllowsSafeFastTrack
        => RequiresAcceptFallback && !HasBlockers;
}
