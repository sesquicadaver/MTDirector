using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Validates an operator-chosen permanent-anchor insertion against an ordered filter snapshot
/// (Onboarding Spec §20–§21 / Issue Set M5-04). Does not pick a “best” position and never
/// stores RouterOS <c>.id</c>.
/// </summary>
public static class AnchorPlacementPlanner
{
    public const string AnalyzerVersion = "mfc.onboarding.anchor_placement.v1";

    private static readonly HashSet<string> TerminalActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "drop",
        "reject",
        "return",
        "accept",
        "fasttrack-connection",
    };

    private static readonly HashSet<string> KnownActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "drop",
        "reject",
        "return",
        "jump",
        "log",
        "passthrough",
        "fasttrack-connection",
    };

    /// <summary>Plans a single family+chain placement from an explicit operator intent.</summary>
    public static AnchorPlacementPlanResult Plan(
        AnchorPlacementIntent intent,
        IReadOnlyList<ActualFilterRule> snapshot)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(snapshot);
        return Evaluate(intent, snapshot, recorded: null);
    }

    /// <summary>
    /// Re-checks a frozen <see cref="AnchorPlacement"/> against a later snapshot (AC#9).
    /// Any ordinal or neighbor-fingerprint drift is <c>ANCHOR_PLACEMENT_STALE</c>.
    /// </summary>
    public static AnchorPlacementPlanResult Revalidate(
        AnchorPlacement placement,
        IReadOnlyList<ActualFilterRule> snapshot)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(snapshot);
        AnchorPlacementIntent intent = AnchorPlacementIntent.Create(
            placement.Family,
            placement.Chain,
            placement.Mode,
            placement.ReferenceRuleFingerprint,
            placement.ReferenceOccurrenceRank);
        return Evaluate(intent, snapshot, placement);
    }

    private static AnchorPlacementPlanResult Evaluate(
        AnchorPlacementIntent intent,
        IReadOnlyList<ActualFilterRule> snapshot,
        AnchorPlacement? recorded)
    {
        List<AnchorPlacementFinding> findings = [];
        string chainName = ChainName(intent.Chain);
        List<ActualFilterRule> chainRules = snapshot
            .Where(r => r.Family == intent.Family
                        && string.Equals(r.Chain, chainName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static r => r.Ordinal)
            .ToList();

        ActualFilterRule? reference = null;
        uint expectedOrdinal;
        if (intent.Mode == AnchorPlacementMode.Append)
        {
            expectedOrdinal = chainRules.Count == 0
                ? 0
                : (uint)(chainRules[^1].Ordinal + 1);
        }
        else
        {
            reference = ResolveReference(intent, chainRules, findings);
            if (reference is null)
            {
                return Finish(findings, null, null);
            }

            expectedOrdinal = (uint)reference.Ordinal;
        }

        ActualFilterRule? predecessor = chainRules.LastOrDefault(r => r.Ordinal < expectedOrdinal);
        ActualFilterRule? successor = intent.Mode == AnchorPlacementMode.Append
            ? null
            : reference;

        Hash256? predFp = predecessor is null ? null : FilterRuleFingerprint.Compute(predecessor);
        Hash256? succFp = successor is null ? null : FilterRuleFingerprint.Compute(successor);

        CheckGuards(chainRules, expectedOrdinal, findings);
        CheckUnreachable(chainRules, expectedOrdinal, findings);
        CheckContext(chainRules, expectedOrdinal, findings);

        if (recorded is not null)
        {
            if (recorded.ExpectedAnchorOrdinal != expectedOrdinal
                || !HashEquals(recorded.ExpectedPredecessorFingerprint, predFp)
                || !HashEquals(recorded.ExpectedSuccessorFingerprint, succFp))
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorPlacementStale,
                    "Filter order or neighbor fingerprints changed; the onboarding plan is stale.",
                    "order"));
            }
        }

        if (findings.Count > 0)
        {
            return Finish(findings, null, null);
        }

        AnchorPlacement placement = AnchorPlacement.Create(
            intent.Family,
            intent.Chain,
            intent.Mode,
            expectedOrdinal,
            intent.ReferenceRuleFingerprint,
            intent.ReferenceOccurrenceRank,
            predFp,
            succFp);

        AnchorPlacementPreview preview = new()
        {
            Family = intent.Family,
            Chain = intent.Chain,
            Mode = intent.Mode,
            ExpectedAnchorOrdinal = expectedOrdinal,
            BeforeLabel = Label(predecessor),
            AfterLabel = Label(successor),
            PredecessorFingerprint = predFp,
            SuccessorFingerprint = succFp,
        };

        return Finish(findings, placement, preview);
    }

    private static ActualFilterRule? ResolveReference(
        AnchorPlacementIntent intent,
        List<ActualFilterRule> chainRules,
        List<AnchorPlacementFinding> findings)
    {
        Hash256 fingerprint = intent.ReferenceRuleFingerprint!;
        uint rank = intent.ReferenceOccurrenceRank!.Value;
        List<ActualFilterRule> matches = [];
        foreach (ActualFilterRule rule in chainRules)
        {
            if (FilterRuleFingerprint.Compute(rule).Equals(fingerprint))
            {
                matches.Add(rule);
            }
        }

        if (rank >= (uint)matches.Count)
        {
            findings.Add(Blocker(
                OnboardingCodes.AnchorReferenceMissing,
                "BEFORE_STATIC_RULE reference fingerprint/rank was not found in the snapshot.",
                "reference"));
            return null;
        }

        ActualFilterRule chosen = matches[(int)rank];
        if (chosen.Dynamic)
        {
            findings.Add(Blocker(
                OnboardingCodes.AnchorReferenceDynamic,
                "Dynamic rule cannot be an anchor placement reference.",
                "reference"));
            return null;
        }

        return chosen;
    }

    private static void CheckGuards(
        List<ActualFilterRule> chainRules,
        uint expectedOrdinal,
        List<AnchorPlacementFinding> findings)
    {
        foreach (ActualFilterRule rule in chainRules)
        {
            if (rule.Disabled || !ActualFilterMarker.IsGuard(rule.Comment))
            {
                continue;
            }

            if (rule.Ordinal >= expectedOrdinal)
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorBeforeGuard,
                    $"Anchor ordinal {expectedOrdinal} is not after management guard at {rule.Ordinal}.",
                    "guard"));
                return;
            }
        }
    }

    private static void CheckUnreachable(
        List<ActualFilterRule> chainRules,
        uint expectedOrdinal,
        List<AnchorPlacementFinding> findings)
    {
        foreach (ActualFilterRule rule in chainRules)
        {
            if (rule.Ordinal >= expectedOrdinal || rule.Disabled)
            {
                continue;
            }

            if (IsUnconditionalTerminal(rule))
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorUnreachable,
                    $"Anchor ordinal {expectedOrdinal} is after unconditional terminal '{rule.Action}' at {rule.Ordinal}.",
                    "terminal"));
                return;
            }
        }
    }

    private static void CheckContext(
        List<ActualFilterRule> chainRules,
        uint expectedOrdinal,
        List<AnchorPlacementFinding> findings)
    {
        foreach (ActualFilterRule rule in chainRules)
        {
            if (rule.Ordinal >= expectedOrdinal || rule.Disabled)
            {
                continue;
            }

            if (rule.UnknownMatchers.Count > 0)
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorContextIndeterminate,
                    $"Unmanaged pre-anchor rule at ordinal {rule.Ordinal} has an unknown matcher.",
                    "context"));
                return;
            }

            if (string.IsNullOrWhiteSpace(rule.Action) || !KnownActions.Contains(rule.Action))
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorContextIndeterminate,
                    $"Pre-anchor rule at ordinal {rule.Ordinal} has an unprovable action '{rule.Action ?? "(missing)"}'.",
                    "context"));
                return;
            }

            if (string.Equals(rule.Action, "jump", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorContextIndeterminate,
                    $"Anchor must not be placed after a jump context at ordinal {rule.Ordinal}.",
                    "context"));
                return;
            }
        }
    }

    private static bool IsUnconditionalTerminal(ActualFilterRule rule)
    {
        if (rule.KnownMatchers.Count > 0 || rule.UnknownMatchers.Count > 0)
        {
            return false;
        }

        return rule.Action is not null && TerminalActions.Contains(rule.Action);
    }

    private static string Label(ActualFilterRule? rule)
    {
        if (rule is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(rule.Comment))
        {
            return rule.Comment.Trim();
        }

        return $"{rule.Action ?? "rule"}@{rule.Ordinal}";
    }

    private static string ChainName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported placement chain '{chain}'."),
        };

    private static bool HashEquals(Hash256? left, Hash256? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Equals(right);
    }

    private static AnchorPlacementFinding Blocker(string code, string message, string? target)
        => new()
        {
            Code = code,
            Severity = OnboardingCodes.SeverityBlocker,
            Message = message,
            Target = target,
        };

    private static AnchorPlacementPlanResult Finish(
        List<AnchorPlacementFinding> findings,
        AnchorPlacement? placement,
        AnchorPlacementPreview? preview)
    {
        IReadOnlyList<AnchorPlacementFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Message, f.Target))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();
        return new AnchorPlacementPlanResult
        {
            Findings = ordered,
            Placement = placement,
            Preview = preview,
        };
    }
}
