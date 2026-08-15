using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Duplicate, shadow, and overlap analysis for pipeline-ordered enabled rules (Policy Model §40–§43 / M2-11).
/// Equal predicates use fail-closed <see cref="PredicateAlgebra.IsSubset"/> both ways — not
/// <see cref="PredicateAlgebra.Relate"/> as exact packet-space EQUAL. Residual subtract is bounded;
/// empty residual without fail-closed cover is <see cref="PolicyAnalysisCodes.ShadowIndeterminate"/>.
/// Caller must supply rules in pipeline order; this type does not reorder across exception revisions.
/// </summary>
public static class PolicySequenceAnalysis
{
    /// <summary>
    /// Analyzes enabled rules in the given order. Disabled rules are ignored (M2-10 already
    /// validated them). Findings are sorted by rule id, code, then related id (AC#12).
    /// </summary>
    public static IReadOnlyList<PolicyAnalysisFinding> Analyze(
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);

        List<PolicyAnalysisFinding> findings = [];
        IEnumerable<IGrouping<(IpAddressFamily Family, PolicyFilterChain Chain), PolicyRule>> surfaces = rules
            .Where(static r => r.Enabled)
            .GroupBy(static r => (r.Family, r.Chain));
        foreach (IGrouping<(IpAddressFamily Family, PolicyFilterChain Chain), PolicyRule> surface in surfaces)
        {
            AnalyzeSurface(surface.ToList(), addresses, services, findings);
        }

        return findings
            .OrderBy(static f => f.RuleId)
            .ThenBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.RelatedRuleId)
            .ToArray();
    }

    private static void AnalyzeSurface(
        IReadOnlyList<PolicyRule> ordered,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services,
        List<PolicyAnalysisFinding> findings)
    {
        List<Prepared> prepared = [];
        foreach (PolicyRule rule in ordered)
        {
            PredicateAlgebraResult normalized = PredicateNormalizer.Normalize(
                rule.Predicate,
                rule.Family,
                rule.Chain,
                addresses,
                services);
            if (normalized.IsFailure)
            {
                findings.Add(Finding(
                    PolicyAnalysisCodes.ShadowIndeterminate,
                    PolicyAnalysisCodes.SeverityBlocker,
                    rule.Id.Value,
                    normalized.Message ?? $"Rule '{rule.Id.Value:D}' could not be normalized for sequence analysis."));
                continue;
            }

            if (normalized.Value is { IsEmpty: true })
            {
                continue;
            }

            prepared.Add(new Prepared(rule, normalized.Value!));
        }

        for (int i = 0; i < prepared.Count; i++)
        {
            Prepared later = prepared[i];
            bool duplicate = false;
            for (int j = 0; j < i; j++)
            {
                Prepared earlier = prepared[j];
                if (later.Rule.Effect.Kind == PolicyRuleEffect.ExemptDenyStage
                    || earlier.Rule.Effect.Kind == PolicyRuleEffect.ExemptDenyStage)
                {
                    continue;
                }

                if (!EqualPredicates(later.Predicate, earlier.Predicate))
                {
                    continue;
                }

                PolicyWitnessPacket? witness = PolicyWitnessPacket.TryFrom(later.Predicate);
                if (SameEffect(later.Rule, earlier.Rule) && SameLogging(later.Rule, earlier.Rule))
                {
                    duplicate = true;
                    findings.Add(Finding(
                        PolicyAnalysisCodes.ExactDuplicate,
                        PolicyAnalysisCodes.SeverityWarning,
                        later.Rule.Id.Value,
                        $"Rule '{later.Rule.Id.Value:D}' is an exact duplicate of '{earlier.Rule.Id.Value:D}'.",
                        earlier.Rule.Id.Value,
                        witness));
                    break;
                }

                if (!SameEffect(later.Rule, earlier.Rule))
                {
                    duplicate = true;
                    findings.Add(Finding(
                        PolicyAnalysisCodes.ConflictingDuplicate,
                        PolicyAnalysisCodes.SeverityBlocker,
                        later.Rule.Id.Value,
                        $"Rule '{later.Rule.Id.Value:D}' matches '{earlier.Rule.Id.Value:D}' but has a different effect.",
                        earlier.Rule.Id.Value,
                        witness));
                    break;
                }
            }

            if (duplicate)
            {
                continue;
            }

            CollectShadow(prepared, i, findings);
            CollectOverlaps(prepared, i, findings);
        }
    }

    private static void CollectShadow(
        IReadOnlyList<Prepared> prepared,
        int index,
        List<PolicyAnalysisFinding> findings)
    {
        Prepared later = prepared[index];
        if (!IsTerminal(later.Rule.Effect.Kind))
        {
            return;
        }

        NormalizedPredicate residual = later.Predicate;
        NormalizedPredicate cover = NormalizedPredicate.Empty;
        Guid? lastTerminal = null;
        for (int j = 0; j < index; j++)
        {
            Prepared earlier = prepared[j];
            if (!IsTerminal(earlier.Rule.Effect.Kind))
            {
                continue;
            }

            lastTerminal = earlier.Rule.Id.Value;
            PredicateAlgebraResult subtracted = PredicateAlgebra.Subtract(residual, earlier.Predicate);
            if (subtracted.IsFailure)
            {
                findings.Add(Finding(
                    PolicyAnalysisCodes.ShadowIndeterminate,
                    PolicyAnalysisCodes.SeverityBlocker,
                    later.Rule.Id.Value,
                    subtracted.Message ?? $"Shadow residual for rule '{later.Rule.Id.Value:D}' exceeded the fragment limit.",
                    earlier.Rule.Id.Value,
                    PolicyWitnessPacket.TryFrom(later.Predicate)));
                return;
            }

            residual = subtracted.Value!;
            PredicateAlgebraResult united = PredicateAlgebra.Union(cover, earlier.Predicate);
            if (united.IsFailure)
            {
                findings.Add(Finding(
                    PolicyAnalysisCodes.ShadowIndeterminate,
                    PolicyAnalysisCodes.SeverityBlocker,
                    later.Rule.Id.Value,
                    united.Message ?? $"Shadow cover for rule '{later.Rule.Id.Value:D}' exceeded the fragment limit.",
                    earlier.Rule.Id.Value,
                    PolicyWitnessPacket.TryFrom(later.Predicate)));
                return;
            }

            cover = united.Value!;
        }

        if (lastTerminal is null)
        {
            return;
        }

        if (residual.IsEmpty)
        {
            if (PredicateAlgebra.IsSubset(later.Predicate, cover))
            {
                findings.Add(Finding(
                    PolicyAnalysisCodes.FullyShadowed,
                    PolicyAnalysisCodes.SeverityBlocker,
                    later.Rule.Id.Value,
                    $"Enabled rule '{later.Rule.Id.Value:D}' is fully shadowed by earlier terminal '{lastTerminal.Value:D}'.",
                    lastTerminal,
                    PolicyWitnessPacket.TryFrom(later.Predicate)));
            }
            else
            {
                findings.Add(Finding(
                    PolicyAnalysisCodes.ShadowIndeterminate,
                    PolicyAnalysisCodes.SeverityBlocker,
                    later.Rule.Id.Value,
                    $"Shadow residual for rule '{later.Rule.Id.Value:D}' is empty without a fail-closed cover.",
                    lastTerminal,
                    PolicyWitnessPacket.TryFrom(later.Predicate)));
            }

            return;
        }

        if (!PredicateAlgebra.IsSubset(later.Predicate, residual))
        {
            findings.Add(Finding(
                PolicyAnalysisCodes.PartiallyShadowed,
                PolicyAnalysisCodes.SeverityWarning,
                later.Rule.Id.Value,
                $"Rule '{later.Rule.Id.Value:D}' is partially shadowed by earlier terminals.",
                lastTerminal,
                PolicyWitnessPacket.TryFrom(residual)));
        }
    }

    private static void CollectOverlaps(
        IReadOnlyList<Prepared> prepared,
        int index,
        List<PolicyAnalysisFinding> findings)
    {
        Prepared later = prepared[index];
        if (later.Rule.Effect.Kind == PolicyRuleEffect.ExemptDenyStage)
        {
            return;
        }

        for (int j = 0; j < index; j++)
        {
            Prepared earlier = prepared[j];
            if (earlier.Rule.Effect.Kind == PolicyRuleEffect.ExemptDenyStage)
            {
                continue;
            }

            if (!PredicateAlgebra.Overlaps(later.Predicate, earlier.Predicate))
            {
                continue;
            }

            string? code = OverlapCode(earlier.Rule.Effect.Kind, later.Rule.Effect.Kind);
            if (code is null)
            {
                continue;
            }

            PredicateAlgebraResult inter = PredicateAlgebra.Intersect(later.Predicate, earlier.Predicate);
            PolicyWitnessPacket? witness = inter.IsSuccess
                ? PolicyWitnessPacket.TryFrom(inter.Value!)
                : PolicyWitnessPacket.TryFrom(later.Predicate);
            string severity = OverlapSeverity(code, earlier.Rule.Effect.Kind, later.Rule.Effect.Kind);
            findings.Add(Finding(
                code,
                severity,
                later.Rule.Id.Value,
                $"Rule '{later.Rule.Id.Value:D}' overlaps earlier '{earlier.Rule.Id.Value:D}' ({code}).",
                earlier.Rule.Id.Value,
                witness));
        }
    }

    private static bool EqualPredicates(NormalizedPredicate left, NormalizedPredicate right)
        => PredicateAlgebra.IsSubset(left, right) && PredicateAlgebra.IsSubset(right, left);

    private static bool SameEffect(PolicyRule left, PolicyRule right)
        => left.Effect.Kind == right.Effect.Kind
           && left.Effect.RejectModeValue == right.Effect.RejectModeValue;

    private static bool SameLogging(PolicyRule left, PolicyRule right)
        => left.Logging.Enabled == right.Logging.Enabled
           && string.Equals(left.Logging.Prefix, right.Logging.Prefix, StringComparison.Ordinal);

    private static bool IsTerminal(PolicyRuleEffect effect)
        => effect is PolicyRuleEffect.Accept
            or PolicyRuleEffect.Drop
            or PolicyRuleEffect.Reject
            or PolicyRuleEffect.FasttrackAccept;

    private static bool IsAllow(PolicyRuleEffect effect)
        => effect is PolicyRuleEffect.Accept or PolicyRuleEffect.FasttrackAccept;

    private static bool IsDeny(PolicyRuleEffect effect)
        => effect is PolicyRuleEffect.Drop or PolicyRuleEffect.Reject;

    private static string? OverlapCode(PolicyRuleEffect earlier, PolicyRuleEffect later)
    {
        if (earlier == PolicyRuleEffect.FasttrackAccept || later == PolicyRuleEffect.FasttrackAccept)
        {
            return PolicyAnalysisCodes.FasttrackOverlap;
        }

        if (IsAllow(earlier) && IsDeny(later))
        {
            return PolicyAnalysisCodes.EarlierAllowBypassesDeny;
        }

        if (IsDeny(earlier) && IsAllow(later))
        {
            return PolicyAnalysisCodes.OrderDependentOverlap;
        }

        if (IsDeny(earlier) && IsDeny(later))
        {
            return earlier == later
                ? PolicyAnalysisCodes.RedundantOverlap
                : PolicyAnalysisCodes.OrderDependentOverlap;
        }

        if (IsAllow(earlier) && IsAllow(later))
        {
            return PolicyAnalysisCodes.RedundantOverlap;
        }

        return null;
    }

    private static string OverlapSeverity(string code, PolicyRuleEffect earlier, PolicyRuleEffect later)
    {
        if (code == PolicyAnalysisCodes.EarlierAllowBypassesDeny)
        {
            return PolicyAnalysisCodes.SeverityBlocker;
        }

        if (code == PolicyAnalysisCodes.FasttrackOverlap && (IsDeny(earlier) || IsDeny(later)))
        {
            return PolicyAnalysisCodes.SeverityBlocker;
        }

        return PolicyAnalysisCodes.SeverityWarning;
    }

    private static PolicyAnalysisFinding Finding(
        string code,
        string severity,
        Guid ruleId,
        string message,
        Guid? related = null,
        PolicyWitnessPacket? witness = null)
        => new()
        {
            Code = code,
            Severity = severity,
            RuleId = ruleId,
            Message = message,
            RelatedRuleId = related,
            Witness = witness,
        };

    private readonly record struct Prepared(PolicyRule Rule, NormalizedPredicate Predicate);
}
