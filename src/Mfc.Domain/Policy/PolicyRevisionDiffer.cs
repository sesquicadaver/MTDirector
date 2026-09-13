using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// UUID-keyed policy revision diff (Policy Model §61). Fuzzy rule matching is forbidden.
/// </summary>
public static class PolicyRevisionDiffer
{
    public static PolicyRevisionDiffResult Diff(
        IReadOnlyList<PolicyRule> before,
        IReadOnlyList<PolicyRule> after,
        IReadOnlyDictionary<AddressObjectId, AddressObject> beforeAddresses,
        IReadOnlyDictionary<AddressObjectId, AddressObject> afterAddresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> beforeServices,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> afterServices,
        IReadOnlySet<Guid> beforeZoneIds,
        IReadOnlySet<Guid> afterZoneIds,
        ChainContractSet? beforeChainContracts = null,
        ChainContractSet? afterChainContracts = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(beforeAddresses);
        ArgumentNullException.ThrowIfNull(afterAddresses);
        ArgumentNullException.ThrowIfNull(beforeServices);
        ArgumentNullException.ThrowIfNull(afterServices);
        ArgumentNullException.ThrowIfNull(beforeZoneIds);
        ArgumentNullException.ThrowIfNull(afterZoneIds);

        Dictionary<Guid, PolicyRule> beforeMap = before.ToDictionary(static r => r.Id.Value);
        Dictionary<Guid, PolicyRule> afterMap = after.ToDictionary(static r => r.Id.Value);
        List<PolicyRuleDiffEntry> ruleChanges = [];
        foreach (Guid id in beforeMap.Keys.Union(afterMap.Keys).OrderBy(static g => g))
        {
            beforeMap.TryGetValue(id, out PolicyRule? left);
            afterMap.TryGetValue(id, out PolicyRule? right);
            List<string> changes = [];
            if (left is null && right is not null)
            {
                changes.Add(PolicyEvidenceAnalysisCodes.ChangeAdded);
            }
            else if (left is not null && right is null)
            {
                changes.Add(PolicyEvidenceAnalysisCodes.ChangeRemoved);
            }
            else if (left is not null && right is not null)
            {
                if (left.Enabled != right.Enabled)
                {
                    changes.Add(right.Enabled
                        ? PolicyEvidenceAnalysisCodes.ChangeEnabled
                        : PolicyEvidenceAnalysisCodes.ChangeDisabled);
                }

                if (left.Ordinal != right.Ordinal || left.Stage != right.Stage || left.Chain != right.Chain)
                {
                    changes.Add(PolicyEvidenceAnalysisCodes.ChangeMoved);
                }

                if (IsModified(left, right))
                {
                    changes.Add(PolicyEvidenceAnalysisCodes.ChangeModified);
                }
            }

            if (changes.Count > 0)
            {
                ruleChanges.Add(new PolicyRuleDiffEntry { RuleId = new RuleId(id), Changes = changes });
            }
        }

        List<PolicyObjectImpact> impacts = [];
        CollectAddressImpacts(beforeAddresses, afterAddresses, after, impacts);
        CollectServiceImpacts(beforeServices, afterServices, after, impacts);
        foreach (Guid zoneId in beforeZoneIds.Union(afterZoneIds).OrderBy(static g => g))
        {
            if (beforeZoneIds.Contains(zoneId) && afterZoneIds.Contains(zoneId))
            {
                continue;
            }

            RuleId[] dependents = after
                .Where(r => ReferencesZone(r, zoneId))
                .Select(static r => r.Id)
                .OrderBy(static r => r.Value)
                .ToArray();
            impacts.Add(new PolicyObjectImpact
            {
                ObjectId = zoneId,
                ObjectKind = "zone",
                DependentRuleIds = dependents,
            });
        }

        (IReadOnlyList<string> packet, IReadOnlyList<string> semantic) = ClassifyPacketSpace(
            before,
            after,
            beforeAddresses,
            afterAddresses,
            beforeServices,
            afterServices);
        List<string> packetClasses = packet.ToList();
        List<string> semanticClasses = semantic.ToList();
        if (ChainDefaultDispositionChanged(beforeChainContracts, afterChainContracts))
        {
            semanticClasses.RemoveAll(static s => s == PolicyEvidenceAnalysisCodes.ClassNoEffectiveChange);
            if (!semanticClasses.Contains(PolicyEvidenceAnalysisCodes.ClassDefaultDisposition, StringComparer.Ordinal))
            {
                semanticClasses.Add(PolicyEvidenceAnalysisCodes.ClassDefaultDisposition);
            }
        }

        return new PolicyRevisionDiffResult
        {
            RuleChanges = ruleChanges
                .OrderBy(static e => e.RuleId.Value)
                .ToArray(),
            ObjectImpacts = impacts
                .OrderBy(static i => i.ObjectKind, StringComparer.Ordinal)
                .ThenBy(static i => i.ObjectId)
                .ToArray(),
            PacketSpaceClasses = packetClasses
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToArray(),
            SemanticClasses = semanticClasses
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static bool IsModified(PolicyRule left, PolicyRule right)
        => left.Family != right.Family
           || left.Effect.Kind != right.Effect.Kind
           || left.Effect.RejectModeValue != right.Effect.RejectModeValue
           || left.Logging.Enabled != right.Logging.Enabled
           || !string.Equals(left.Logging.Prefix, right.Logging.Prefix, StringComparison.Ordinal)
           || left.ExceptionEligible != right.ExceptionEligible
           || !string.Equals(left.Description, right.Description, StringComparison.Ordinal)
           || !SamePredicate(left.Predicate, right.Predicate);

    private static bool SamePredicate(TrafficPredicate left, TrafficPredicate right)
        => ReferenceEquals(left, right)
           || (SameAddressSelector(left.SourceAddresses, right.SourceAddresses)
               && SameAddressSelector(left.DestinationAddresses, right.DestinationAddresses)
               && SameZoneSelector(left.IngressZones, right.IngressZones)
               && SameZoneSelector(left.EgressZones, right.EgressZones)
               && SameServiceSelector(left.Services, right.Services)
               && SameList(left.ConnectionStates, right.ConnectionStates)
               && SameList(left.ConnectionNatStates, right.ConnectionNatStates)
               && SameList(left.SourceAddressTypes, right.SourceAddressTypes)
               && SameList(left.DestinationAddressTypes, right.DestinationAddressTypes)
               && SameTcpFlags(left.TcpFlags, right.TcpFlags)
               && SameIpsec(left.IpsecPolicy, right.IpsecPolicy));

    private static bool SameAddressSelector(AddressSelector? left, AddressSelector? right)
        => ReferenceEquals(left, right)
           || (left is not null
               && right is not null
               && SameIds(left.Include, right.Include, static id => id.Value)
               && SameIds(left.Exclude, right.Exclude, static id => id.Value));

    private static bool SameZoneSelector(ZoneSelector? left, ZoneSelector? right)
        => ReferenceEquals(left, right)
           || (left is not null
               && right is not null
               && SameIds(left.Include, right.Include, static id => id.Value)
               && SameIds(left.Exclude, right.Exclude, static id => id.Value));

    private static bool SameServiceSelector(ServiceSelector? left, ServiceSelector? right)
        => ReferenceEquals(left, right)
           || (left is not null
               && right is not null
               && SameIds(left.Include, right.Include, static id => id.Value));

    private static bool SameIds<T>(IReadOnlyList<T> left, IReadOnlyList<T> right, Func<T, Guid> id)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return left.Select(id).OrderBy(static g => g).SequenceEqual(right.Select(id).OrderBy(static g => g));
    }

    private static bool SameTcpFlags(TcpFlagConstraint? left, TcpFlagConstraint? right)
        => ReferenceEquals(left, right)
           || (left is not null
               && right is not null
               && left.RequiredPresent.SequenceEqual(right.RequiredPresent)
               && left.RequiredAbsent.SequenceEqual(right.RequiredAbsent));

    private static bool SameIpsec(IpsecPolicyPredicate? left, IpsecPolicyPredicate? right)
        => ReferenceEquals(left, right)
           || (left is not null
               && right is not null
               && left.Direction == right.Direction
               && left.Policy == right.Policy);

    private static bool SameList<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        where T : struct
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    private static void CollectAddressImpacts(
        IReadOnlyDictionary<AddressObjectId, AddressObject> before,
        IReadOnlyDictionary<AddressObjectId, AddressObject> after,
        IReadOnlyList<PolicyRule> afterRules,
        List<PolicyObjectImpact> impacts)
    {
        foreach (AddressObjectId id in before.Keys.Union(after.Keys).OrderBy(static k => k.Value))
        {
            before.TryGetValue(id, out AddressObject? left);
            after.TryGetValue(id, out AddressObject? right);
            if (left is not null && right is not null && SameAddress(left, right))
            {
                continue;
            }

            RuleId[] dependents = afterRules
                .Where(r => ReferencesAddress(r, id))
                .Select(static r => r.Id)
                .OrderBy(static r => r.Value)
                .ToArray();
            impacts.Add(new PolicyObjectImpact
            {
                ObjectId = id.Value,
                ObjectKind = "address",
                DependentRuleIds = dependents,
            });
        }
    }

    private static void CollectServiceImpacts(
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> before,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> after,
        IReadOnlyList<PolicyRule> afterRules,
        List<PolicyObjectImpact> impacts)
    {
        foreach (ServiceObjectId id in before.Keys.Union(after.Keys).OrderBy(static k => k.Value))
        {
            before.TryGetValue(id, out ServiceObject? left);
            after.TryGetValue(id, out ServiceObject? right);
            if (left is not null && right is not null && SameService(left, right))
            {
                continue;
            }

            RuleId[] dependents = afterRules
                .Where(r => r.Predicate.Services is not null && r.Predicate.Services.Include.Contains(id))
                .Select(static r => r.Id)
                .OrderBy(static r => r.Value)
                .ToArray();
            impacts.Add(new PolicyObjectImpact
            {
                ObjectId = id.Value,
                ObjectKind = "service",
                DependentRuleIds = dependents,
            });
        }
    }

    private static bool SameAddress(AddressObject left, AddressObject right)
        => left.Family == right.Family
           && left.Name.Value == right.Name.Value
           && left.Intervals.SequenceEqual(right.Intervals);

    private static bool SameService(ServiceObject left, ServiceObject right)
        => left.Name.Value == right.Name.Value
           && left.Terms.SequenceEqual(right.Terms);

    private static bool ReferencesAddress(PolicyRule rule, AddressObjectId id)
        => ContainsAddress(rule.Predicate.SourceAddresses, id)
           || ContainsAddress(rule.Predicate.DestinationAddresses, id);

    private static bool ContainsAddress(AddressSelector? selector, AddressObjectId id)
        => selector is not null
           && (selector.Include.Contains(id) || selector.Exclude.Contains(id));

    private static bool ReferencesZone(PolicyRule rule, Guid zoneId)
        => ContainsZone(rule.Predicate.IngressZones, zoneId)
           || ContainsZone(rule.Predicate.EgressZones, zoneId);

    private static bool ContainsZone(ZoneSelector? selector, Guid zoneId)
        => selector is not null
           && (selector.Include.Any(z => z.Value == zoneId) || selector.Exclude.Any(z => z.Value == zoneId));

    private static (IReadOnlyList<string> Packet, IReadOnlyList<string> Semantic) ClassifyPacketSpace(
        IReadOnlyList<PolicyRule> before,
        IReadOnlyList<PolicyRule> after,
        IReadOnlyDictionary<AddressObjectId, AddressObject> beforeAddresses,
        IReadOnlyDictionary<AddressObjectId, AddressObject> afterAddresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> beforeServices,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> afterServices)
    {
        NormalizedPredicate? acceptBefore = FirstMatchAcceptedSpace(before, beforeAddresses, beforeServices);
        NormalizedPredicate? acceptAfter = FirstMatchAcceptedSpace(after, afterAddresses, afterServices);
        if (acceptBefore is null || acceptAfter is null)
        {
            // Undetermined packet-space proof (audit §06 / AUDIT-DIFF-01).
            return ([], [PolicyEvidenceAnalysisCodes.ProofIndeterminate]);
        }

        PredicateRelation relation = PredicateAlgebra.Relate(acceptBefore, acceptAfter);
        List<string> packet = [];
        List<string> semantic = [];
        switch (relation)
        {
            case PredicateRelation.Equal:
            case PredicateRelation.Empty:
                semantic.Add(PolicyEvidenceAnalysisCodes.ClassNoEffectiveChange);
                break;
            case PredicateRelation.Subset:
                packet.Add(PolicyEvidenceAnalysisCodes.PacketNewlyAccepted);
                semantic.Add(PolicyEvidenceAnalysisCodes.ClassPermissive);
                break;
            case PredicateRelation.Superset:
                packet.Add(PolicyEvidenceAnalysisCodes.PacketNewlyDenied);
                semantic.Add(PolicyEvidenceAnalysisCodes.ClassRestrictive);
                break;
            case PredicateRelation.PartialOverlap:
            case PredicateRelation.Disjoint:
                packet.Add(PolicyEvidenceAnalysisCodes.PacketNewlyAccepted);
                packet.Add(PolicyEvidenceAnalysisCodes.PacketNewlyDenied);
                semantic.Add(PolicyEvidenceAnalysisCodes.ClassMixed);
                break;
            default:
                semantic.Add(PolicyEvidenceAnalysisCodes.ClassMixed);
                break;
        }

        bool rejectChanged = RejectSignature(before) != RejectSignature(after);
        if (rejectChanged)
        {
            packet.Add(PolicyEvidenceAnalysisCodes.PacketRejectChanged);
        }

        return (packet.Distinct(StringComparer.Ordinal).OrderBy(static s => s, StringComparer.Ordinal).ToArray(),
            semantic.Distinct(StringComparer.Ordinal).OrderBy(static s => s, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// First-match accepted packet space per family/chain surface (AUDIT-DIFF-01).
    /// Earlier terminal DROP/REJECT covers traffic so later ACCEPT cannot claim it.
    /// </summary>
    private static NormalizedPredicate? FirstMatchAcceptedSpace(
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        NormalizedPredicate total = NormalizedPredicate.Empty;
        foreach ((IpAddressFamily family, PolicyFilterChain chain) in rules
                     .Select(static r => (r.Family, r.Chain))
                     .Distinct()
                     .OrderBy(static s => s.Family)
                     .ThenBy(static s => s.Chain))
        {
            NormalizedPredicate? surface = FirstMatchAcceptedOnSurface(
                rules.Where(r => r.Family == family && r.Chain == chain),
                family,
                chain,
                addresses,
                services);
            if (surface is null)
            {
                return null;
            }

            PredicateAlgebraResult union = PredicateAlgebra.Union(total, surface);
            if (union.IsFailure || union.Value is null)
            {
                return null;
            }

            total = union.Value;
        }

        return total;
    }

    private static NormalizedPredicate? FirstMatchAcceptedOnSurface(
        IEnumerable<PolicyRule> rules,
        IpAddressFamily family,
        PolicyFilterChain chain,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        NormalizedPredicate covered = NormalizedPredicate.Empty;
        NormalizedPredicate accepted = NormalizedPredicate.Empty;
        foreach (PolicyRule rule in rules
                     .Where(static r => r.Enabled)
                     .OrderBy(static r => PolicyPipelineV1.Ordinal(r.Stage))
                     .ThenBy(static r => r.Ordinal)
                     .ThenBy(static r => r.Id.Value))
        {
            PredicateAlgebraResult normalized = PredicateNormalizer.Normalize(
                rule.Predicate,
                family,
                chain,
                addresses,
                services);
            if (normalized.IsFailure || normalized.Value is null)
            {
                return null;
            }

            PredicateAlgebraResult reaches = PredicateAlgebra.Subtract(normalized.Value, covered);
            if (reaches.IsFailure || reaches.Value is null)
            {
                return null;
            }

            bool isAllow = rule.Effect.Kind is PolicyRuleEffect.Accept or PolicyRuleEffect.FasttrackAccept;
            if (isAllow)
            {
                PredicateAlgebraResult acceptUnion = PredicateAlgebra.Union(accepted, reaches.Value);
                if (acceptUnion.IsFailure || acceptUnion.Value is null)
                {
                    return null;
                }

                accepted = acceptUnion.Value;
            }

            PredicateAlgebraResult coverUnion = PredicateAlgebra.Union(covered, reaches.Value);
            if (coverUnion.IsFailure || coverUnion.Value is null)
            {
                return null;
            }

            covered = coverUnion.Value;
        }

        return accepted;
    }

    private static bool ChainDefaultDispositionChanged(ChainContractSet? before, ChainContractSet? after)
    {
        if (before is null || after is null)
        {
            return false;
        }

        Dictionary<(IpAddressFamily Family, PolicyFilterChain Chain), ChainContract> beforeMap =
            before.Items.ToDictionary(static c => (c.Family, c.Chain));
        Dictionary<(IpAddressFamily Family, PolicyFilterChain Chain), ChainContract> afterMap =
            after.Items.ToDictionary(static c => (c.Family, c.Chain));
        foreach ((IpAddressFamily Family, PolicyFilterChain Chain) key in beforeMap.Keys.Union(afterMap.Keys))
        {
            beforeMap.TryGetValue(key, out ChainContract? left);
            afterMap.TryGetValue(key, out ChainContract? right);
            if (left is null || right is null)
            {
                return true;
            }

            if (left.DefaultDisposition != right.DefaultDisposition
                || left.RejectModeValue != right.RejectModeValue)
            {
                return true;
            }
        }

        return false;
    }

    private static string RejectSignature(IReadOnlyList<PolicyRule> rules)
        => string.Join(
            '|',
            rules.Where(static r => r.Enabled && r.Effect.Kind == PolicyRuleEffect.Reject)
                .OrderBy(static r => r.Id.Value)
                .Select(static r => $"{r.Id}:{r.Effect.RejectModeValue}"));
}
