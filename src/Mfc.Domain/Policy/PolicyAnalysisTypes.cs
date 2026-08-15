namespace Mfc.Domain.Policy;

/// <summary>One structural, satisfiability, or sequence finding with a frozen code.</summary>
public sealed class PolicyAnalysisFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required Guid RuleId { get; init; }

    public required string Message { get; init; }

    /// <summary>Earlier rule that proves duplicate, shadow, or overlap; null for unary findings.</summary>
    public Guid? RelatedRuleId { get; init; }

    /// <summary>Concrete representative packet when the finding is proven (Policy Model §43).</summary>
    public PolicyWitnessPacket? Witness { get; init; }
}

/// <summary>
/// Optional M2-10 test seam. Production compose does not use this hook: it calls
/// <see cref="PolicySequenceAnalysis"/> after pipeline order and exception insertion.
/// </summary>
public delegate IReadOnlyList<PolicyAnalysisFinding> PolicySequenceAnalyzer(
    IReadOnlyList<PolicyRule> structurallyValidRules);

/// <summary>Outcome of <see cref="PolicyAnalysisEngine.Analyze"/>.</summary>
public sealed class PolicyAnalysisResult
{
    public required IReadOnlyList<PolicyAnalysisFinding> Findings { get; init; }

    /// <summary>True only when <see cref="PolicySequenceAnalyzer"/> ran (AC#12).</summary>
    public required bool SequenceAnalyzerInvoked { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == PolicyAnalysisCodes.SeverityBlocker);

    public PolicyAnalysisFinding? FirstBlocker
        => Findings.FirstOrDefault(static f => f.Severity == PolicyAnalysisCodes.SeverityBlocker);
}
