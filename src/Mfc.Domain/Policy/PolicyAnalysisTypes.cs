namespace Mfc.Domain.Policy;

/// <summary>One structural or satisfiability finding with a frozen code (M2-10).</summary>
public sealed class PolicyAnalysisFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required Guid RuleId { get; init; }

    public required string Message { get; init; }
}

/// <summary>
/// Sequence-level analyzer hook (M2-11). Invoked only after structural/satisfiability blockers
/// are absent so invalid rules never reach shadow/overlap analysis.
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
