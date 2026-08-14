using System.Text.Json;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Pure logical policy composer for a Node (Policy Model §§29–34.1 / M2-07).
/// No VRRP role, WAN, device, or zone-binding inputs; exceptions are out of scope (count slot = 0).
/// </summary>
public static class EffectivePolicyComposer
{
    /// <summary>
    /// Composes company (required) with optional site/node overlays into an ephemeral effective policy.
    /// </summary>
    /// <param name="company">Company baseline layer; null → <c>POLICY_COMPOSE_COMPANY_REQUIRED</c>.</param>
    /// <param name="site">Optional site overlay.</param>
    /// <param name="node">Optional node overlay.</param>
    /// <param name="nodeId">Target node id (visibility for NODE-owned objects).</param>
    /// <param name="siteId">Parent site id (visibility for SITE-owned objects from NODE rules).</param>
    /// <param name="knownZoneIds">Zone catalog ids from Application (<see cref="ZoneDefinition"/> store).</param>
    public static PolicyComposeResult Compose(
        PolicyLayer? company,
        PolicyLayer? site,
        PolicyLayer? node,
        Guid nodeId,
        Guid? siteId,
        IReadOnlySet<Guid> knownZoneIds)
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

        Dictionary<Guid, ParsedObject> addresses = [];
        Dictionary<Guid, ParsedObject> services = [];
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

        List<PolicyRule> active = [];
        foreach ((PolicyLayer layer, PolicyRule rule) in allRules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

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

            active.Add(rule);
        }

        PolicyRule[] orderedActive = OrderActive(active);
        JsonElement[] mergedAddresses = addresses.Values
            .OrderBy(static o => o.Id)
            .Select(static o => o.Element)
            .ToArray();
        JsonElement[] mergedServices = services.Values
            .OrderBy(static o => o.Id)
            .Select(static o => o.Element)
            .ToArray();

        List<PolicyComposeFinding> findings = CollectUnused(addresses, services, orderedActive);
        Hash256 logicalHash = HashComposed(
            company,
            site,
            node,
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
        Dictionary<Guid, ParsedObject> addresses,
        Dictionary<Guid, ParsedObject> services)
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
        Dictionary<Guid, ParsedObject> target,
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

            target[id] = new ParsedObject(id, identityResult.Identity!, element.Clone());
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
        Dictionary<Guid, ParsedObject> addresses,
        Dictionary<Guid, ParsedObject> services)
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
                if (!services.TryGetValue(serviceId.Value, out ParsedObject? parsed))
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
        Dictionary<Guid, ParsedObject> addresses,
        AddressConsumerContext consumer,
        Guid ruleId)
    {
        foreach (AddressObjectId id in selector.Include.Concat(selector.Exclude))
        {
            if (!addresses.TryGetValue(id.Value, out ParsedObject? parsed))
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
        Dictionary<Guid, ParsedObject> addresses,
        Dictionary<Guid, ParsedObject> services,
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
            exceptionCount: 0,
            objectBytes,
            ruleBytes,
            chainBytes);
    }

    private static PolicyRule[] OrderActive(IReadOnlyList<PolicyRule> active)
        => active
            .OrderBy(static r => r.Family)
            .ThenBy(static r => r.Chain)
            .ThenBy(static r => PolicyPipelineV1.Ordinal(r.Stage))
            .ThenBy(static r => r.Ordinal)
            .ThenBy(static r => r.Id.Value)
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

    private sealed record ParsedObject(Guid Id, PolicyObjectIdentity Identity, JsonElement Element);

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
