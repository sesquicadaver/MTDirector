using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>Shared helpers for policy revision load, CAS, and catalog checks (M2-06).</summary>
internal static class PolicyRevisionSupport
{
    public const string SoftCatalogWarningCode = "POLICY_SELECTOR_CATALOG_SOFT";

    public static ApplicationError? EnsureContentHash(PolicyRevision revision, byte[] expectedContentHash)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(expectedContentHash);
        if (expectedContentHash.Length != Hash256.Size)
        {
            return ApplicationError.Validation(
                $"expected_content_hash must be exactly {Hash256.Size} bytes (SHA-256).");
        }

        Hash256 expected = Hash256.Create(expectedContentHash);
        if (!revision.ContentHash.Equals(expected))
        {
            return ApplicationError.Conflict(
                "Policy revision content_hash mismatch (expected_content_hash CAS).");
        }

        return null;
    }

    public static ApplicationError? EnsureEditable(PolicyRevision revision)
    {
        if (revision.State is not (PolicyRevisionState.Draft or PolicyRevisionState.Validated))
        {
            return ApplicationError.Validation(
                $"Only DRAFT (or VALIDATED returning to DRAFT) revisions may be edited; actual {revision.State}.");
        }

        return null;
    }

    public static async Task<(PolicyRevision? Revision, ApplicationError? Error)> LoadRevisionAsync(
        IPolicyStore store,
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        PolicyRevision? revision = await store
            .GetRevisionAsync(new PolicyRevisionId(revisionId), cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            return (null, ApplicationError.NotFound($"Policy revision '{revisionId}' not found."));
        }

        return (revision, null);
    }

    public static ApplicationResult<PolicyDocument> ReadDocument(PolicyRevision revision)
    {
        try
        {
            return ApplicationResults.Ok(PolicyDocumentReader.Read(revision.CanonicalBytes));
        }
        catch (Domain.DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }

    public static async Task<ApplicationError?> EnsureZonesExistAsync(
        IZoneDefinitionStore zones,
        TrafficPredicate predicate,
        CancellationToken cancellationToken)
    {
        foreach (ZoneId zoneId in EnumerateZoneIds(predicate))
        {
            ZoneDefinition? zone = await zones.GetAsync(zoneId, cancellationToken).ConfigureAwait(false);
            if (zone is null)
            {
                return ApplicationError.NotFound($"Zone '{zoneId}' referenced by rule selector was not found.");
            }
        }

        return null;
    }

    /// <summary>
    /// LOCK-5: when document address/service arrays are empty → soft warning;
    /// when non-empty → referenced UUIDs must appear in those arrays (hard error).
    /// </summary>
    public static ApplicationError? EnsureAddressServiceCatalog(
        PolicyDocument document,
        TrafficPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(predicate);

        HashSet<Guid> addressCatalog = ExtractObjectIds(document.AddressObjects);
        HashSet<Guid> serviceCatalog = ExtractObjectIds(document.ServiceObjects);

        if (addressCatalog.Count > 0)
        {
            foreach (Guid id in EnumerateAddressIds(predicate))
            {
                if (!addressCatalog.Contains(id))
                {
                    return ApplicationError.Validation(
                        $"Address object '{id:D}' is not present in document address_objects catalog.");
                }
            }
        }

        if (serviceCatalog.Count > 0)
        {
            foreach (Guid id in EnumerateServiceIds(predicate))
            {
                if (!serviceCatalog.Contains(id))
                {
                    return ApplicationError.Validation(
                        $"Service object '{id:D}' is not present in document service_objects catalog.");
                }
            }
        }

        return null;
    }

    public static IReadOnlyList<PolicyWarningView> CollectSoftCatalogWarnings(
        PolicyDocument document,
        TrafficPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(predicate);

        List<PolicyWarningView> warnings = [];
        bool addressCatalogEmpty = document.AddressObjects.Count == 0;
        bool serviceCatalogEmpty = document.ServiceObjects.Count == 0;

        if (addressCatalogEmpty
            && (HasAddressIds(predicate.SourceAddresses) || HasAddressIds(predicate.DestinationAddresses)))
        {
            warnings.Add(new PolicyWarningView
            {
                Code = SoftCatalogWarningCode,
                Message =
                    "Address object catalog is empty in the revision document; selector ids are accepted without hard validation.",
                Subject = "address",
            });
        }

        if (serviceCatalogEmpty && predicate.Services is not null && predicate.Services.Include.Count > 0)
        {
            warnings.Add(new PolicyWarningView
            {
                Code = SoftCatalogWarningCode,
                Message =
                    "Service object catalog is empty in the revision document; selector ids are accepted without hard validation.",
                Subject = "service",
            });
        }

        return warnings;
    }

    public static IReadOnlyList<PolicyWarningView> MergeWarnings(IEnumerable<PolicyRuleView> rules)
    {
        List<PolicyWarningView> merged = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (PolicyRuleView rule in rules)
        {
            foreach (PolicyWarningView warning in rule.Warnings)
            {
                string key = $"{warning.Code}|{warning.Subject}|{warning.Message}";
                if (seen.Add(key))
                {
                    merged.Add(warning);
                }
            }
        }

        return merged;
    }

    private static IEnumerable<ZoneId> EnumerateZoneIds(TrafficPredicate predicate)
    {
        if (predicate.IngressZones is not null)
        {
            foreach (ZoneId id in predicate.IngressZones.Include)
            {
                yield return id;
            }

            foreach (ZoneId id in predicate.IngressZones.Exclude)
            {
                yield return id;
            }
        }

        if (predicate.EgressZones is not null)
        {
            foreach (ZoneId id in predicate.EgressZones.Include)
            {
                yield return id;
            }

            foreach (ZoneId id in predicate.EgressZones.Exclude)
            {
                yield return id;
            }
        }
    }

    private static bool HasAddressIds(AddressSelector? selector)
        => selector is not null && (selector.Include.Count > 0 || selector.Exclude.Count > 0);

    private static IEnumerable<Guid> EnumerateAddressIds(TrafficPredicate predicate)
    {
        foreach (AddressObjectId id in predicate.SourceAddresses?.Include ?? [])
        {
            yield return id.Value;
        }

        foreach (AddressObjectId id in predicate.SourceAddresses?.Exclude ?? [])
        {
            yield return id.Value;
        }

        foreach (AddressObjectId id in predicate.DestinationAddresses?.Include ?? [])
        {
            yield return id.Value;
        }

        foreach (AddressObjectId id in predicate.DestinationAddresses?.Exclude ?? [])
        {
            yield return id.Value;
        }
    }

    private static IEnumerable<Guid> EnumerateServiceIds(TrafficPredicate predicate)
    {
        if (predicate.Services is null)
        {
            yield break;
        }

        foreach (ServiceObjectId id in predicate.Services.Include)
        {
            yield return id.Value;
        }
    }

    private static HashSet<Guid> ExtractObjectIds(IReadOnlyList<System.Text.Json.JsonElement> elements)
    {
        HashSet<Guid> ids = [];
        foreach (System.Text.Json.JsonElement element in elements)
        {
            if (element.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                continue;
            }

            if (element.TryGetProperty("id", out System.Text.Json.JsonElement idElement)
                && idElement.ValueKind == System.Text.Json.JsonValueKind.String
                && Guid.TryParse(idElement.GetString(), out Guid id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
