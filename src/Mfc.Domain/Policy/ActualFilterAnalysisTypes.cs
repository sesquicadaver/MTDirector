using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>One CFG node (rule, managed pipeline, return, or RouterOS implicit accept).</summary>
public sealed class ActualFilterGraphNode
{
    public required string Id { get; init; }

    public required ActualFilterGraphNodeKind Kind { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required string Chain { get; init; }

    public int? Ordinal { get; init; }

    public string? Action { get; init; }
}

/// <summary>Directed CFG edge.</summary>
public sealed class ActualFilterGraphEdge
{
    public required string FromId { get; init; }

    public required string ToId { get; init; }

    public required ActualFilterGraphEdgeKind Kind { get; init; }
}

public enum ActualFilterGraphNodeKind : byte
{
    Rule = 0,
    ManagedPipeline = 1,
    Return = 2,
    RouterOsImplicitAccept = 3,
}

public enum ActualFilterGraphEdgeKind : byte
{
    Fallthrough = 0,
    Jump = 1,
    Return = 2,
    Terminal = 3,
}

/// <summary>Bounded actual-filter CFG (Policy Model §45.1).</summary>
public sealed class ActualFilterGraph
{
    public required IReadOnlyList<ActualFilterGraphNode> Nodes { get; init; }

    public required IReadOnlyList<ActualFilterGraphEdge> Edges { get; init; }
}

/// <summary>One actual-filter / pre-anchor finding. Subject is family/chain/ordinal, not a UUID.</summary>
public sealed class ActualFilterFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public IpAddressFamily? Family { get; init; }

    public string? Chain { get; init; }

    public int? Ordinal { get; init; }
}

/// <summary>Outcome of <see cref="ActualFilterAnalysis.Analyze"/>.</summary>
public sealed class ActualFilterAnalysisResult
{
    public required IReadOnlyList<ActualFilterFinding> Findings { get; init; }

    public required ActualFilterGraph Graph { get; init; }

    /// <summary>SHA-256 of the actual filter context (Policy Model §34.3 slot).</summary>
    public required Hash256 ActualContextHash { get; init; }

    /// <summary>SHA-256 analysis-context preimage that includes <see cref="ActualContextHash"/>.</summary>
    public required Hash256 AnalysisContextHash { get; init; }

    /// <summary>True when candidate chain contracts include RETURN_TO_UNMANAGED and post-anchor was walked.</summary>
    public required bool PostAnchorAnalyzed { get; init; }

    /// <summary>
    /// Always false: RouterOS built-in fallthrough ACCEPT is never the managed default
    /// (Policy Model §15 / M2-12 AC#11).
    /// </summary>
    public required bool UsesRouterOsImplicitAcceptAsManagedDefault { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == ActualFilterAnalysisCodes.SeverityBlocker);
}
