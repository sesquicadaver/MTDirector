using System.Text.Json;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Pure logical policy composer for a Node (Policy Model §§29–34.1 / M2-07 + M2-08 exceptions).
/// No VRRP role, WAN, device, zone-binding, or clock inputs.
/// </summary>
public static class EffectivePolicyComposer
{
    /// <summary>
    /// Composes company (required) with optional site/node overlays and exception layers
    /// into an ephemeral effective policy.
    /// </summary>
    public static PolicyComposeResult Compose(
        PolicyLayer? company,
        PolicyLayer? site,
        PolicyLayer? node,
        Guid nodeId,
        Guid? siteId,
        IReadOnlySet<Guid> knownZoneIds,
        IReadOnlyList<PolicyLayer>? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(knownZoneIds);
        if (company is null)
        {
            return PolicyComposeResult.Fail(
                PolicyComposeCodes.CompanyRequired,
                "Company baseline is required for logical policy composition.");
        }

        PolicyComposeResult? parentError = VerifyParentContext(company, site, node);
        if (parentError is not null)
        {
            return parentError;
        }

        Dictionary<Guid, ComposedPolicyObject> addresses = [];
        Dictionary<Guid, ComposedPolicyObject> services = [];
        PolicyComposeResult? objectError = MergeObjects(company, site, node, addresses, services);
        if (objectError is not null)
        {
            return objectError;
        }

        List<(PolicyLayer Layer, PolicyRule Rule)> allRules = [];
        HashSet<Guid> ruleIds = [];
        foreach (PolicyLayer layer in EnumerateLayers(company, site, node))
        {
            foreach (PolicyRule rule in layer.PolicyDocument.Rules)
            {
                if (!ruleIds.Add(rule.Id.Value))
                {
                    return PolicyComposeResult.Fail(
                        PolicyComposeCodes.UuidCollision,
                        $"Rule UUID '{rule.Id.Value:D}' collides across policy layers.");
                }

                allRules.Add((layer, rule));
            }
        }

        foreach ((PolicyLayer layer, PolicyRule rule) in allRules)
        {
            if (!PolicyPipelineV1.IsOwnerEffectAllowed(rule.Stage, layer.Kind, layer.OwnerScope, rule.Effect.Kind))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.StageOwnership,
                    $"Rule '{rule.Id.Value:D}' stage {PolicyPipelineV1.FormatStage(rule.Stage)} is not owned by " +
                    $"{PolicyCanonicalWriter.FormatKind(layer.Kind)}/{PolicyCanonicalWriter.FormatOwnerScope(layer.OwnerScope)}.");
            }

            PolicyComposeResult? selectorError = ValidateSelectors(
                layer,
                rule,
                nodeId,
                siteId,
                knownZoneIds,
                addresses,
                services);
            if (selectorError is not null)
            {
                return selectorError;
            }
        }

        PolicyComposeResult? analysisError = AnalyzeRules(
            allRules.Select(static t => t.Rule).ToArray(),
            addresses,
            services,
            knownZoneIds);
        if (analysisError is not null)
        {
            return analysisError;
        }

        List<PolicyRule> active = [];
        foreach ((_, PolicyRule rule) in allRules)
        {
            if (rule.Enabled)
            {
                active.Add(rule);
            }
        }

        List<ActiveEntry> entries = active
            .Select(static r => new ActiveEntry(r, Guid.Empty, Guid.Empty))
            .ToList();
        IReadOnlyList<PolicyLayer> exceptionLayers = exceptions ?? [];
        foreach (PolicyLayer exception in exceptionLayers)
        {
            PolicyComposeResult? exceptionError = ExceptionComposeProof.Evaluate(
                exception,
                company,
                site,
                node,
                allRules.Select(static t => t.Rule).ToArray(),
                active,
                addresses,
                services,
                out PolicyRule? exemptRule);
            if (exceptionError is not null)
            {
                return exceptionError;
            }

            PolicyComposeResult? selectorError = ValidateSelectors(
                exception,
                exemptRule!,
                nodeId,
                siteId,
                knownZoneIds,
                addresses,
                services);
            if (selectorError is not null)
            {
                return selectorError;
            }

            PolicyComposeResult? exemptAnalysis = AnalyzeRules(
                [exemptRule!],
                addresses,
                services,
                knownZoneIds);
            if (exemptAnalysis is not null)
            {
                return exemptAnalysis;
            }

            if (!ruleIds.Add(exemptRule!.Id.Value))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.UuidCollision,
                    $"Rule UUID '{exemptRule.Id.Value:D}' collides across policy layers.");
            }

            entries.Add(new ActiveEntry(exemptRule, exception.RevisionId, exception.PolicyId));
        }

        PolicyRule[] orderedActive = OrderActive(entries);
        JsonElement[] mergedAddresses = addresses.Values
            .OrderBy(static o => o.Id)
            .Select(static o => o.Element)
            .ToArray();
        JsonElement[] mergedServices = services.Values
            .OrderBy(static o => o.Id)
            .Select(static o => o.Element)
            .ToArray();

        List<PolicyComposeFinding> findings = CollectUnused(addresses, services, orderedActive);
        Hash256[] exceptionHashes = exceptionLayers
            .OrderBy(static e => e.PolicyDocument.ExceptionMetadata!.WaivedRuleId.Value)
            .ThenBy(static e => e.PolicyId)
            .Select(static e => e.ContentHash)
            .ToArray();
        Hash256 logicalHash = HashComposed(
            company,
            site,
            node,
            exceptionHashes,
            mergedAddresses,
            mergedServices,
            orderedActive);

        return PolicyComposeResult.Ok(new ComposedEffectivePolicy
        {
            LogicalEffectiveHash = logicalHash,
            ActiveRules = orderedActive,
            MergedAddressObjects = mergedAddresses,
            MergedServiceObjects = mergedServices,
            Findings = findings,
        });
    }

    private static PolicyComposeResult? AnalyzeRules(
        PolicyRule[] rules,
        Dictionary<Guid, ComposedPolicyObject> addresses,
        Dictionary<Guid, ComposedPolicyObject> services,
        IReadOnlySet<Guid> knownZoneIds)
    {
        if (rules.Length == 0)
        {
            return null;
        }

        string? catalogError = PredicateCatalogBuilder.TryBuild(
            rules.Select(static r => r.Predicate),
            addresses,
            services,
            out Dictionary<AddressObjectId, AddressObject> typedAddresses,
            out Dictionary<ServiceObjectId, ServiceObject> typedServices);
        if (catalogError is not null)
        {
            return PolicyComposeResult.Fail(PolicyComposeCodes.SelectorUnresolved, catalogError);
        }

        PolicyAnalysisResult analysis = PolicyAnalysisEngine.Analyze(
            rules,
            typedAddresses,
            typedServices,
            knownZoneIds);
        PolicyAnalysisFinding? blocker = analysis.FirstBlocker;
        return blocker is null ? null : PolicyComposeResult.Fail(blocker.Code, blocker.Message);
    }

    private static PolicyComposeResult? VerifyParentContext(
        PolicyLayer company,
        PolicyLayer? site,
        PolicyLayer? node)
    {
        Hash256? expectedCompany = PolicyHashing.ComputeParentContextHash(
            PolicyKind.CompanyBaseline,
            company.ContentHash,
            siteOverlayHash: null,
            nodeOverlayHash: null,
            waivedRuleHash: null);
        if (!HashEquals(expectedCompany, company.ParentContextHash))
        {
            return PolicyComposeResult.Fail(
                PolicyComposeCodes.ParentContextMismatch,
                "Company baseline parent_context_hash does not match loaded parent content hashes.");
        }

        if (site is not null)
        {
            Hash256? expectedSite = PolicyHashing.ComputeParentContextHash(
                site.Kind,
                company.ContentHash,
                siteOverlayHash: null,
                nodeOverlayHash: null,
                waivedRuleHash: null);
            if (!HashEquals(expectedSite, site.ParentContextHash))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.ParentContextMismatch,
                    "Site overlay parent_context_hash does not match loaded parent content hashes.");
            }
        }

        if (node is not null)
        {
            Hash256? expectedNode = PolicyHashing.ComputeParentContextHash(
                node.Kind,
                company.ContentHash,
                site?.ContentHash,
                nodeOverlayHash: null,
                waivedRuleHash: null);
            if (!HashEquals(expectedNode, node.ParentContextHash))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.ParentContextMismatch,
                    "Node overlay parent_context_hash does not match loaded parent content hashes.");
            }
        }

        return null;
    }

    private static bool HashEquals(Hash256? expected, Hash256? actual)
    {
        if (expected is null && actual is null)
        {
            return true;
        }

        if (expected is null || actual is null)
        {
            return false;
        }

        return expected.Equals(actual);
    }

    private static PolicyComposeResult? MergeObjects(
        PolicyLayer company,
        PolicyLayer? site,
        PolicyLayer? node,
        Dictionary<Guid, ComposedPolicyObject> addresses,
        Dictionary<Guid, ComposedPolicyObject> services)
    {
        foreach (PolicyLayer layer in EnumerateLayers(company, site, node))
        {
            PolicyComposeResult? addressError = IngestObjects(
                layer,
                layer.PolicyDocument.AddressObjects,
                addresses,
                "address");
            if (addressError is not null)
            {
                return addressError;
            }

            PolicyComposeResult? serviceError = IngestObjects(
                layer,
                layer.PolicyDocument.ServiceObjects,
                services,
                "service");
            if (serviceError is not null)
            {
                return serviceError;
            }
        }

        return null;
    }

    private static PolicyComposeResult? IngestObjects(
        PolicyLayer layer,
        IReadOnlyList<JsonElement> elements,
        Dictionary<Guid, ComposedPolicyObject> target,
        string kindLabel)
    {
        foreach (JsonElement element in elements)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("id", out JsonElement idElement)
                || idElement.ValueKind != JsonValueKind.String
                || !TryParseUuid(idElement.GetString(), out Guid id))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.ObjectIdMalformed,
                    $"Each non-empty {kindLabel} object must be a JSON object with a parseable UUID id.");
            }

            if (target.ContainsKey(id))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.UuidCollision,
                    $"{kindLabel} object UUID '{id:D}' collides across policy layers.");
            }

            IdentityParseResult identityResult = ResolveIdentity(layer, element, id);
            if (identityResult.IsFailure)
            {
                return identityResult.Failure;
            }

            target[id] = new ComposedPolicyObject(id, identityResult.Identity!, element.Clone());
        }

        return null;
    }

    private static IdentityParseResult ResolveIdentity(PolicyLayer layer, JsonElement element, Guid id)
    {
        PolicyObjectOwnerScope inheritedScope = ToObjectScope(layer.OwnerScope);
        Guid? inheritedOwnerId = layer.OwnerId;
        bool hasScope = element.TryGetProperty("owner_scope", out JsonElement scopeElement);
        bool hasOwnerId = element.TryGetProperty("owner_id", out JsonElement ownerIdElement);

        PolicyObjectOwnerScope scope = inheritedScope;
        Guid? ownerId = inheritedOwnerId;
        if (hasScope)
        {
            if (scopeElement.ValueKind != JsonValueKind.String
                || !TryParseObjectScope(scopeElement.GetString(), out scope))
            {
                return IdentityParseResult.Fail(
                    PolicyComposeCodes.Visibility,
                    $"Object '{id:D}' owner_scope is not visible to its source revision.");
            }
        }

        if (hasOwnerId)
        {
            if (ownerIdElement.ValueKind == JsonValueKind.Null)
            {
                ownerId = null;
            }
            else if (ownerIdElement.ValueKind != JsonValueKind.String
                     || !TryParseUuid(ownerIdElement.GetString(), out Guid parsedOwner))
            {
                return IdentityParseResult.Fail(
                    PolicyComposeCodes.Visibility,
                    $"Object '{id:D}' owner_id is not visible to its source revision.");
            }
            else
            {
                ownerId = parsedOwner;
            }
        }

        if (scope != inheritedScope || ownerId != inheritedOwnerId)
        {
            return IdentityParseResult.Fail(
                PolicyComposeCodes.Visibility,
                $"Object '{id:D}' owner does not match its source revision (upward or cross-scope ownership is forbidden).");
        }

        return IdentityParseResult.Ok(new PolicyObjectIdentity(id, scope, ownerId));
    }

    private static PolicyComposeResult? ValidateSelectors(
        PolicyLayer layer,
        PolicyRule rule,
        Guid nodeId,
        Guid? siteId,
        IReadOnlySet<Guid> knownZoneIds,
        Dictionary<Guid, ComposedPolicyObject> addresses,
        Dictionary<Guid, ComposedPolicyObject> services)
    {
        AddressConsumerContext consumer = new()
        {
            Scope = ToObjectScope(layer.OwnerScope),
            OwnerId = layer.Kind == PolicyKind.NodeOverlay ? nodeId : layer.OwnerId,
            SiteId = siteId,
        };

        if (rule.Predicate.SourceAddresses is not null)
        {
            PolicyComposeResult? error = ValidateAddressSelector(
                rule.Predicate.SourceAddresses, addresses, consumer, rule.Id.Value);
            if (error is not null)
            {
                return error;
            }
        }

        if (rule.Predicate.DestinationAddresses is not null)
        {
            PolicyComposeResult? error = ValidateAddressSelector(
                rule.Predicate.DestinationAddresses, addresses, consumer, rule.Id.Value);
            if (error is not null)
            {
                return error;
            }
        }

        if (rule.Predicate.Services is not null)
        {
            foreach (ServiceObjectId serviceId in rule.Predicate.Services.Include)
            {
                if (!services.TryGetValue(serviceId.Value, out ComposedPolicyObject? parsed))
                {
                    return PolicyComposeResult.Fail(
                        PolicyComposeCodes.SelectorUnresolved,
                        $"Service selector UUID '{serviceId.Value:D}' on rule '{rule.Id.Value:D}' is unresolved.");
                }

                if (!ServiceObjectVisibility.CanReference(consumer, parsed.Identity))
                {
                    return PolicyComposeResult.Fail(
                        PolicyComposeCodes.Visibility,
                        $"Service object '{serviceId.Value:D}' is not visible to rule '{rule.Id.Value:D}'.");
                }
            }
        }

        PolicyComposeResult? zoneError = ValidateZones(rule.Predicate.IngressZones, knownZoneIds, rule.Id.Value);
        if (zoneError is not null)
        {
            return zoneError;
        }

        return ValidateZones(rule.Predicate.EgressZones, knownZoneIds, rule.Id.Value);
    }

    private static PolicyComposeResult? ValidateAddressSelector(
        AddressSelector selector,
        Dictionary<Guid, ComposedPolicyObject> addresses,
        AddressConsumerContext consumer,
        Guid ruleId)
    {
        foreach (AddressObjectId id in selector.Include.Concat(selector.Exclude))
        {
            if (!addresses.TryGetValue(id.Value, out ComposedPolicyObject? parsed))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.SelectorUnresolved,
                    $"Address selector UUID '{id.Value:D}' on rule '{ruleId:D}' is unresolved.");
            }

            if (!AddressObjectVisibility.CanReference(consumer, parsed.Identity))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.Visibility,
                    $"Address object '{id.Value:D}' is not visible to rule '{ruleId:D}'.");
            }
        }

        return null;
    }

    private static PolicyComposeResult? ValidateZones(ZoneSelector? selector, IReadOnlySet<Guid> knownZoneIds, Guid ruleId)
    {
        if (selector is null)
        {
            return null;
        }

        foreach (ZoneId id in selector.Include.Concat(selector.Exclude))
        {
            if (!knownZoneIds.Contains(id.Value))
            {
                return PolicyComposeResult.Fail(
                    PolicyComposeCodes.ZoneNotFound,
                    $"Zone '{id.Value:D}' referenced by rule '{ruleId:D}' was not found in the zone catalog.");
            }
        }

        return null;
    }

    private static List<PolicyComposeFinding> CollectUnused(
        Dictionary<Guid, ComposedPolicyObject> addresses,
        Dictionary<Guid, ComposedPolicyObject> services,
        IReadOnlyList<PolicyRule> activeRules)
    {
        HashSet<Guid> usedAddresses = [];
        HashSet<Guid> usedServices = [];
        foreach (PolicyRule rule in activeRules)
        {
            CollectAddressIds(rule.Predicate.SourceAddresses, usedAddresses);
            CollectAddressIds(rule.Predicate.DestinationAddresses, usedAddresses);
            if (rule.Predicate.Services is not null)
            {
                foreach (ServiceObjectId id in rule.Predicate.Services.Include)
                {
                    usedServices.Add(id.Value);
                }
            }
        }

        List<PolicyComposeFinding> findings = [];
        foreach (Guid id in addresses.Keys.Where(id => !usedAddresses.Contains(id)).OrderBy(static g => g))
        {
            findings.Add(new PolicyComposeFinding
            {
                Code = PolicyComposeCodes.UnusedPolicyObject,
                Message = $"Address object '{id:D}' is not referenced by any enabled rule.",
                Subject = id.ToString("D"),
            });
        }

        foreach (Guid id in services.Keys.Where(id => !usedServices.Contains(id)).OrderBy(static g => g))
        {
            findings.Add(new PolicyComposeFinding
            {
                Code = PolicyComposeCodes.UnusedPolicyObject,
                Message = $"Service object '{id:D}' is not referenced by any enabled rule.",
                Subject = id.ToString("D"),
            });
        }

        return findings;
    }

    private static void CollectAddressIds(AddressSelector? selector, HashSet<Guid> used)
    {
        if (selector is null)
        {
            return;
        }

        foreach (AddressObjectId id in selector.Include.Concat(selector.Exclude))
        {
            used.Add(id.Value);
        }
    }

    private static Hash256 HashComposed(
        PolicyLayer company,
        PolicyLayer? site,
        PolicyLayer? node,
        IReadOnlyList<Hash256> exceptionHashes,
        IReadOnlyList<JsonElement> mergedAddresses,
        IReadOnlyList<JsonElement> mergedServices,
        IReadOnlyList<PolicyRule> orderedActive)
    {
        List<byte[]> objectBytes = [];
        foreach (JsonElement element in mergedAddresses.Concat(mergedServices))
        {
            objectBytes.Add(JsonSerializer.SerializeToUtf8Bytes(element));
        }

        byte[][] ruleBytes = orderedActive.Select(PolicyCanonicalWriter.WriteRuleBytes).ToArray();
        byte[] chainBytes = PolicyCanonicalWriter.WriteChainContractSetBytes(company.PolicyDocument.ChainContracts);
        return PolicyHashing.HashLogicalEffective(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            site?.ContentHash,
            node?.ContentHash,
            exceptionHashes,
            objectBytes,
            ruleBytes,
            chainBytes);
    }

    private static PolicyRule[] OrderActive(IReadOnlyList<ActiveEntry> entries)
        => entries
            .OrderBy(static e => e.Rule.Family)
            .ThenBy(static e => e.Rule.Chain)
            .ThenBy(static e => PolicyPipelineV1.Ordinal(e.Rule.Stage))
            .ThenBy(static e => e.RevisionId)
            .ThenBy(static e => e.Rule.Ordinal)
            .ThenBy(static e => e.Rule.Id.Value)
            .Select(static e => e.Rule)
            .ToArray();

    private static IEnumerable<PolicyLayer> EnumerateLayers(PolicyLayer company, PolicyLayer? site, PolicyLayer? node)
    {
        yield return company;
        if (site is not null)
        {
            yield return site;
        }

        if (node is not null)
        {
            yield return node;
        }
    }

    private static PolicyObjectOwnerScope ToObjectScope(PolicyOwnerScope scope)
        => scope switch
        {
            PolicyOwnerScope.Company => PolicyObjectOwnerScope.Company,
            PolicyOwnerScope.Site => PolicyObjectOwnerScope.Site,
            PolicyOwnerScope.Node => PolicyObjectOwnerScope.Node,
            _ => PolicyObjectOwnerScope.Company,
        };

    private static bool TryParseObjectScope(string? text, out PolicyObjectOwnerScope scope)
    {
        switch (text)
        {
            case "COMPANY":
                scope = PolicyObjectOwnerScope.Company;
                return true;
            case "SITE":
                scope = PolicyObjectOwnerScope.Site;
                return true;
            case "NODE":
                scope = PolicyObjectOwnerScope.Node;
                return true;
            case "EXCEPTION":
                scope = PolicyObjectOwnerScope.Exception;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    private static bool TryParseUuid(string? text, out Guid value)
    {
        if (!string.IsNullOrWhiteSpace(text)
            && (Guid.TryParseExact(text, "D", out value) || Guid.TryParse(text, out value)))
        {
            return true;
        }

        value = default;
        return false;
    }

    private readonly record struct ActiveEntry(PolicyRule Rule, Guid RevisionId, Guid PolicyId);

    private readonly struct IdentityParseResult
    {
        private IdentityParseResult(PolicyObjectIdentity? identity, PolicyComposeResult? failure)
        {
            Identity = identity;
            Failure = failure;
        }

        public PolicyObjectIdentity? Identity { get; }

        public PolicyComposeResult? Failure { get; }

        public bool IsFailure => Failure is not null;

        public static IdentityParseResult Ok(PolicyObjectIdentity identity)
            => new(identity, null);

        public static IdentityParseResult Fail(string code, string message)
            => new(null, PolicyComposeResult.Fail(code, message));
    }
}
