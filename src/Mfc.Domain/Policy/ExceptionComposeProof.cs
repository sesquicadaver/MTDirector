using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Compose-time proofs for one EXCEPTION layer (M2-08 + M2-09 interval subset/overlap).</summary>
public static class ExceptionComposeProof
{
    /// <summary>
    /// Validates one exception layer against overlay rules and parent hashes.
    /// Target lookup includes disabled overlay rules so a disabled waived rule is
    /// <see cref="PolicyExceptionCodes.TargetNotEligible"/> rather than not-found.
    /// On success <paramref name="exemptRule"/> is the single enabled EXEMPT rule.
    /// </summary>
    public static PolicyComposeResult? Evaluate(
        PolicyLayer exception,
        PolicyLayer company,
        PolicyLayer? site,
        PolicyLayer? node,
        IReadOnlyList<PolicyRule> overlayRules,
        IReadOnlyList<PolicyRule> overlayActive,
        IReadOnlyDictionary<Guid, ComposedPolicyObject> addresses,
        IReadOnlyDictionary<Guid, ComposedPolicyObject> services,
        out PolicyRule? exemptRule)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(overlayRules);
        ArgumentNullException.ThrowIfNull(overlayActive);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);
        exemptRule = null;

        if (exception.Kind != PolicyKind.Exception)
        {
            return Fail(PolicyExceptionCodes.MetadataInvalid, "Exception layer kind must be EXCEPTION.");
        }

        if (exception.PolicyDocument.AddressObjects.Count > 0
            || exception.PolicyDocument.ServiceObjects.Count > 0)
        {
            return Fail(
                PolicyExceptionCodes.ObjectsForbidden,
                "EXCEPTION revisions cannot define address_objects or service_objects in this milestone.");
        }

        ExceptionMetadata? metadata = exception.PolicyDocument.ExceptionMetadata;
        if (metadata is null)
        {
            return Fail(
                PolicyExceptionCodes.MetadataInvalid,
                "APPROVED EXCEPTION revision requires typed exception_metadata.");
        }

        if (metadata.TargetScope != exception.OwnerScope
            || metadata.TargetScopeId != exception.OwnerId)
        {
            return Fail(
                PolicyExceptionCodes.MetadataInvalid,
                "exception_metadata target_scope/target_scope_id must match the EXCEPTION policy owner.");
        }

        PolicyRule[] enabled = exception.PolicyDocument.Rules.Where(static r => r.Enabled).ToArray();
        if (enabled.Length != 1)
        {
            return Fail(
                PolicyExceptionCodes.RuleCount,
                "EXCEPTION document must have exactly one enabled rule.");
        }

        PolicyRule rule = enabled[0];
        if (rule.Effect.Kind != PolicyRuleEffect.ExemptDenyStage)
        {
            return Fail(
                PolicyExceptionCodes.Effect,
                "EXCEPTION effect must be EXEMPT_DENY_STAGE.");
        }

        if (!PolicyPipelineV1.TryExemptionTwin(metadata.TargetStage, out PolicyPipelineStage twin))
        {
            return Fail(
                PolicyExceptionCodes.StageMismatch,
                "EXCEPTION target_stage has no exemption twin.");
        }

        if (rule.Stage != twin)
        {
            return Fail(
                PolicyExceptionCodes.StageMismatch,
                "EXCEPTION rule stage must be the exemption twin of target_stage.");
        }

        if (!PolicyPipelineV1.IsOwnerEffectAllowed(
                rule.Stage, exception.Kind, exception.OwnerScope, rule.Effect.Kind))
        {
            return Fail(
                PolicyExceptionCodes.StageOwnership,
                "EXCEPTION owner cannot place EXEMPT_DENY_STAGE in this exemption stage.");
        }

        PolicyRule? target = overlayRules.FirstOrDefault(r => r.Id == metadata.WaivedRuleId);
        if (target is null)
        {
            return Fail(
                PolicyExceptionCodes.TargetNotFound,
                $"Waived rule '{metadata.WaivedRuleId}' was not found in the composed overlay.");
        }

        if (target.Stage == PolicyPipelineStage.MandatoryPreStateDeny)
        {
            return Fail(
                PolicyExceptionCodes.MandatoryDeny,
                "Mandatory deny cannot have an exception.");
        }

        if (metadata.TargetStage != target.Stage)
        {
            return Fail(
                PolicyExceptionCodes.StageMismatch,
                "exception_metadata.target_stage must equal the waived rule stage.");
        }

        if (!target.Enabled
            || !target.ExceptionEligible
            || target.Effect.Kind is not (PolicyRuleEffect.Drop or PolicyRuleEffect.Reject))
        {
            return Fail(
                PolicyExceptionCodes.TargetNotEligible,
                "Waived rule must be enabled, exception_eligible, and DROP or REJECT.");
        }

        if (rule.Family != target.Family || rule.Chain != target.Chain)
        {
            return Fail(
                PolicyExceptionCodes.FamilyChainMismatch,
                "EXCEPTION family and chain must match the waived rule.");
        }

        List<TrafficPredicate> needed = [rule.Predicate, target.Predicate];
        foreach (PolicyRule other in overlayActive)
        {
            if (other.Id == target.Id
                || other.Family != target.Family
                || other.Chain != target.Chain
                || other.Stage != target.Stage
                || other.Effect.Kind is not (PolicyRuleEffect.Drop or PolicyRuleEffect.Reject))
            {
                continue;
            }

            needed.Add(other.Predicate);
        }

        string? catalogError = PredicateCatalogBuilder.TryBuild(
            needed,
            addresses,
            services,
            out Dictionary<AddressObjectId, AddressObject> addressCatalog,
            out Dictionary<ServiceObjectId, ServiceObject> serviceCatalog);
        if (catalogError is not null)
        {
            return Fail(PolicyComposeCodes.SelectorUnresolved, catalogError);
        }

        string? subset = ExceptionPredicateProof.CheckSubset(
            rule.Predicate,
            target.Predicate,
            rule.Family,
            rule.Chain,
            addressCatalog,
            serviceCatalog);
        if (subset is not null)
        {
            return Fail(
                subset,
                subset == PredicateAlgebraCodes.ComplexityLimit
                    ? "EXCEPTION predicate expansion exceeded the bounded algebra limit."
                    : "EXCEPTION predicate is not a fail-closed subset of the waived rule.");
        }

        foreach (PolicyRule other in overlayActive)
        {
            if (other.Id == target.Id)
            {
                continue;
            }

            if (other.Family != target.Family
                || other.Chain != target.Chain
                || other.Stage != target.Stage)
            {
                continue;
            }

            if (other.Effect.Kind is not (PolicyRuleEffect.Drop or PolicyRuleEffect.Reject))
            {
                continue;
            }

            string? overlapError = ExceptionPredicateProof.CheckOverlap(
                rule.Predicate,
                other.Predicate,
                rule.Family,
                rule.Chain,
                addressCatalog,
                serviceCatalog);
            if (overlapError is not null)
            {
                return Fail(
                    overlapError,
                    overlapError == PredicateAlgebraCodes.ComplexityLimit
                        ? "EXCEPTION overlap proof exceeded the bounded algebra limit."
                        : $"EXCEPTION overlaps non-target deny '{other.Id}' in the same stage.");
            }
        }

        Hash256 waivedHash = PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(target));
        Hash256? nodeHash = exception.OwnerScope == PolicyOwnerScope.Node ? node?.ContentHash : null;
        Hash256 expectedParent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception,
            company.ContentHash,
            site?.ContentHash,
            nodeHash,
            waivedHash)!;
        if (exception.ParentContextHash is null || !expectedParent.Equals(exception.ParentContextHash))
        {
            return Fail(
                PolicyExceptionCodes.ParentContextMismatch,
                "EXCEPTION parent_context_hash does not match company/site/node/waived-rule hashes.");
        }

        exemptRule = rule;
        return null;
    }

    private static PolicyComposeResult Fail(string code, string message)
        => PolicyComposeResult.Fail(code, message);
}
