using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Helpers for contiguous ordinals and draft list mutations within a policy document
/// (Policy Model §23; M2-06 AC#6–#8).
/// </summary>
public static class PolicyRuleSet
{
    /// <summary>
    /// Ensures ordinals within each (family, chain, stage) group are contiguous from 0.
    /// When family/chain/stage are provided, only that group is checked.
    /// </summary>
    public static void EnsureContiguousOrdinals(
        IReadOnlyList<PolicyRule> rules,
        IpAddressFamily? family = null,
        PolicyFilterChain? chain = null,
        PolicyPipelineStage? stage = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureUniqueIds(rules);

        IEnumerable<IGrouping<(IpAddressFamily Family, PolicyFilterChain Chain, PolicyPipelineStage Stage), PolicyRule>> groups =
            rules
                .Where(r =>
                    (family is null || r.Family == family)
                    && (chain is null || r.Chain == chain)
                    && (stage is null || r.Stage == stage))
                .GroupBy(static r => (r.Family, r.Chain, r.Stage));

        foreach (IGrouping<(IpAddressFamily Family, PolicyFilterChain Chain, PolicyPipelineStage Stage), PolicyRule> group in groups)
        {
            PolicyRule[] ordered = group.OrderBy(static r => r.Ordinal).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].Ordinal != (uint)i)
                {
                    throw new DomainInvariantException(
                        $"Rule ordinals must be contiguous within family/chain/stage " +
                        $"({PolicyPipelineV1.FormatFamily(group.Key.Family)}/" +
                        $"{PolicyPipelineV1.FormatFilterChain(group.Key.Chain)}/" +
                        $"{PolicyPipelineV1.FormatStage(group.Key.Stage)}); " +
                        $"expected {i}, found {ordered[i].Ordinal}.");
                }
            }
        }
    }

    /// <summary>Enabled rules only (disabled rules are excluded from active evaluation).</summary>
    public static IReadOnlyList<PolicyRule> ActiveRules(IReadOnlyList<PolicyRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Where(static r => r.Enabled).ToArray();
    }

    /// <summary>Appends a rule and renumbers its (family, chain, stage) group contiguously.</summary>
    public static IReadOnlyList<PolicyRule> WithAdd(IReadOnlyList<PolicyRule> rules, PolicyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(rule);
        if (rules.Any(r => r.Id == rule.Id))
        {
            throw new DomainInvariantException($"Rule id '{rule.Id}' already exists.");
        }

        List<PolicyRule> next = [.. rules, rule];
        return RenumberGroup(next, rule.Family, rule.Chain, rule.Stage);
    }

    /// <summary>Replaces a rule by id and renumbers its group (and old group if stage moved).</summary>
    public static IReadOnlyList<PolicyRule> WithUpdate(IReadOnlyList<PolicyRule> rules, PolicyRule updated)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(updated);
        int index = IndexOf(rules, updated.Id);
        PolicyRule previous = rules[index];
        List<PolicyRule> next = rules.ToList();
        next[index] = updated;
        next = RenumberGroup(next, previous.Family, previous.Chain, previous.Stage).ToList();
        if (previous.Family != updated.Family
            || previous.Chain != updated.Chain
            || previous.Stage != updated.Stage)
        {
            next = RenumberGroup(next, updated.Family, updated.Chain, updated.Stage).ToList();
        }

        return next;
    }

    /// <summary>Removes a rule by id and renumbers its former group.</summary>
    public static IReadOnlyList<PolicyRule> WithDelete(IReadOnlyList<PolicyRule> rules, RuleId id)
    {
        ArgumentNullException.ThrowIfNull(rules);
        int index = IndexOf(rules, id);
        PolicyRule removed = rules[index];
        List<PolicyRule> next = rules.Where((_, i) => i != index).ToList();
        return RenumberGroup(next, removed.Family, removed.Chain, removed.Stage);
    }

    /// <summary>
    /// Reorders rules inside one (family, chain, stage) group to the given id sequence
    /// and assigns contiguous ordinals 0..n-1.
    /// </summary>
    public static IReadOnlyList<PolicyRule> WithReorder(
        IReadOnlyList<PolicyRule> rules,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        IReadOnlyList<RuleId> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(orderedIds);

        List<PolicyRule> group = rules
            .Where(r => r.Family == family && r.Chain == chain && r.Stage == stage)
            .ToList();
        if (group.Count != orderedIds.Count)
        {
            throw new DomainInvariantException(
                "Reorder id list must contain exactly the rules in the target family/chain/stage group.");
        }

        HashSet<Guid> expected = group.Select(static r => r.Id.Value).ToHashSet();
        HashSet<Guid> seen = [];
        List<PolicyRule> reordered = [];
        foreach (RuleId id in orderedIds)
        {
            if (!seen.Add(id.Value) || !expected.Contains(id.Value))
            {
                throw new DomainInvariantException("Reorder id list is invalid for the target group.");
            }

            reordered.Add(group.Single(r => r.Id == id));
        }

        Dictionary<Guid, PolicyRule> replacement = [];
        for (int i = 0; i < reordered.Count; i++)
        {
            replacement[reordered[i].Id.Value] = reordered[i].WithOrdinal((uint)i);
        }

        return rules.Select(r =>
            r.Family == family && r.Chain == chain && r.Stage == stage
                ? replacement[r.Id.Value]
                : r).ToArray();
    }

    private static PolicyRule[] RenumberGroup(
        IReadOnlyList<PolicyRule> rules,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage)
    {
        List<PolicyRule> group = rules
            .Where(r => r.Family == family && r.Chain == chain && r.Stage == stage)
            .OrderBy(static r => r.Ordinal)
            .ThenBy(static r => r.Id.Value)
            .ToList();
        Dictionary<Guid, PolicyRule> replacement = [];
        for (int i = 0; i < group.Count; i++)
        {
            replacement[group[i].Id.Value] = group[i].WithOrdinal((uint)i);
        }

        return rules.Select(r =>
            r.Family == family && r.Chain == chain && r.Stage == stage
                ? replacement[r.Id.Value]
                : r).ToArray();
    }

    private static int IndexOf(IReadOnlyList<PolicyRule> rules, RuleId id)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Id == id)
            {
                return i;
            }
        }

        throw new DomainInvariantException($"Rule id '{id}' was not found.");
    }

    private static void EnsureUniqueIds(IReadOnlyList<PolicyRule> rules)
    {
        HashSet<Guid> seen = [];
        foreach (PolicyRule rule in rules)
        {
            if (!seen.Add(rule.Id.Value))
            {
                throw new DomainInvariantException($"Duplicate rule id '{rule.Id}'.");
            }
        }
    }
}
