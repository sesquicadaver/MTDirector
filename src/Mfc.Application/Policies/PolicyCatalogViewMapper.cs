using System.Net;
using System.Text.Json;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>Maps opaque catalog JSON / chain contracts onto revision views (M2-18).</summary>
internal static class PolicyCatalogViewMapper
{
    public static IReadOnlyList<AddressObjectView> MapAddresses(IReadOnlyList<JsonElement> elements)
    {
        List<AddressObjectView> views = [];
        foreach (JsonElement element in elements)
        {
            if (TryMapAddress(element, out AddressObjectView? view) && view is not null)
            {
                views.Add(view);
            }
        }

        return views;
    }

    public static IReadOnlyList<ServiceObjectView> MapServices(IReadOnlyList<JsonElement> elements)
    {
        List<ServiceObjectView> views = [];
        foreach (JsonElement element in elements)
        {
            if (TryMapService(element, out ServiceObjectView? view) && view is not null)
            {
                views.Add(view);
            }
        }

        return views;
    }

    public static IReadOnlyList<ChainContractView> MapChainContracts(ChainContractSet contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        return contracts.Items.Select(static c => new ChainContractView
        {
            Family = c.Family,
            Chain = c.Chain,
            DefaultDisposition = PolicyPipelineV1.FormatDisposition(c.DefaultDisposition),
            RejectMode = c.RejectModeValue,
        }).ToArray();
    }

    public static string SerializeTests(IReadOnlyList<JsonElement> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (JsonElement test in tests)
            {
                test.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static PolicyObjectIdentity DeriveObjectIdentity(
        Policy policy,
        PolicyRevision revision)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(revision);
        return policy.Kind switch
        {
            PolicyKind.CompanyBaseline => new PolicyObjectIdentity(
                Guid.Empty, PolicyObjectOwnerScope.Company, null),
            PolicyKind.SiteOverlay => new PolicyObjectIdentity(
                Guid.Empty, PolicyObjectOwnerScope.Site, policy.OwnerId),
            PolicyKind.NodeOverlay => new PolicyObjectIdentity(
                Guid.Empty, PolicyObjectOwnerScope.Node, policy.OwnerId),
            PolicyKind.Exception => new PolicyObjectIdentity(
                Guid.Empty,
                PolicyObjectOwnerScope.Exception,
                policy.OwnerId,
                revision.Id),
            PolicyKind.IncidentDenyOverlay => new PolicyObjectIdentity(
                Guid.Empty,
                PolicyObjectOwnerScope.Node,
                policy.OwnerId,
                revision.Id),
            _ => throw new Domain.DomainInvariantException($"Unknown policy kind '{policy.Kind}'."),
        };
    }

    public static PolicyObjectIdentity WithId(PolicyObjectIdentity template, Guid id)
        => new(id, template.OwnerScope, template.OwnerId, template.ExceptionRevisionId);

    public static bool TryParseTypedAddresses(
        PolicyDocument document,
        PolicyObjectIdentity ownerTemplate,
        out Dictionary<AddressObjectId, AddressObject> addresses,
        out string? error)
    {
        addresses = [];
        error = null;
        foreach (JsonElement element in document.AddressObjects)
        {
            if (!TryReadId(element, out Guid id))
            {
                error = "Address object catalog entry is missing a valid id.";
                return false;
            }

            PolicyObjectIdentity identity = WithId(ownerTemplate, id);
            if (!PolicyObjectJsonReader.TryReadAddress(element, identity, out AddressObject? parsed, out error)
                || parsed is null)
            {
                return false;
            }

            addresses[parsed.Id] = parsed;
        }

        return true;
    }

    public static bool TryParseTypedServices(
        PolicyDocument document,
        PolicyObjectIdentity ownerTemplate,
        out Dictionary<ServiceObjectId, ServiceObject> services,
        out string? error)
    {
        services = [];
        error = null;
        foreach (JsonElement element in document.ServiceObjects)
        {
            if (!TryReadId(element, out Guid id))
            {
                error = "Service object catalog entry is missing a valid id.";
                return false;
            }

            PolicyObjectIdentity identity = WithId(ownerTemplate, id);
            if (!PolicyObjectJsonReader.TryReadService(element, identity, out ServiceObject? parsed, out error)
                || parsed is null)
            {
                return false;
            }

            services[parsed.Id] = parsed;
        }

        return true;
    }

    public static HashSet<Guid> ExtractZoneIds(PolicyDocument document)
    {
        HashSet<Guid> ids = [];
        foreach (JsonElement zone in document.ZoneDefinitions)
        {
            if (TryReadId(zone, out Guid id))
            {
                ids.Add(id);
            }
        }

        foreach (PolicyRule rule in document.Rules)
        {
            CollectZones(rule.Predicate.IngressZones, ids);
            CollectZones(rule.Predicate.EgressZones, ids);
        }

        return ids;
    }

    private static void CollectZones(ZoneSelector? selector, HashSet<Guid> ids)
    {
        if (selector is null)
        {
            return;
        }

        foreach (ZoneId id in selector.Include.Concat(selector.Exclude))
        {
            ids.Add(id.Value);
        }
    }

    private static bool TryMapAddress(JsonElement element, out AddressObjectView? view)
    {
        view = null;
        if (element.ValueKind != JsonValueKind.Object
            || !TryReadId(element, out Guid id)
            || !element.TryGetProperty("name", out JsonElement nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(nameElement.GetString())
            || !element.TryGetProperty("family", out JsonElement familyElement)
            || familyElement.ValueKind != JsonValueKind.String
            || !TryParseFamily(familyElement.GetString(), out IpAddressFamily family)
            || !element.TryGetProperty("entries", out JsonElement entriesElement)
            || entriesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<AddressObjectEntryView> entries = [];
        foreach (JsonElement entry in entriesElement.EnumerateArray())
        {
            if (!TryMapAddressEntry(entry, out AddressObjectEntryView? mapped) || mapped is null)
            {
                return false;
            }

            entries.Add(mapped);
        }

        string? description = null;
        if (element.TryGetProperty("description", out JsonElement descriptionElement)
            && descriptionElement.ValueKind == JsonValueKind.String)
        {
            description = descriptionElement.GetString();
        }

        view = new AddressObjectView
        {
            Id = id,
            Name = nameElement.GetString()!,
            Family = family,
            Entries = entries,
            Description = description,
        };
        return true;
    }

    private static bool TryMapAddressEntry(JsonElement element, out AddressObjectEntryView? view)
    {
        view = null;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("kind", out JsonElement kindElement)
            || kindElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? kind = kindElement.GetString();
        view = kind switch
        {
            "HOST" => new AddressObjectEntryView
            {
                Kind = "HOST",
                Address = element.GetProperty("address").GetString(),
            },
            "PREFIX" => new AddressObjectEntryView
            {
                Kind = "PREFIX",
                Address = element.GetProperty("address").GetString(),
                PrefixLength = element.GetProperty("prefix_length").GetByte(),
            },
            "RANGE" => new AddressObjectEntryView
            {
                Kind = "RANGE",
                Start = element.GetProperty("start").GetString(),
                End = element.GetProperty("end").GetString(),
            },
            _ => null,
        };
        return view is not null;
    }

    private static bool TryMapService(JsonElement element, out ServiceObjectView? view)
    {
        view = null;
        if (element.ValueKind != JsonValueKind.Object
            || !TryReadId(element, out Guid id)
            || !element.TryGetProperty("name", out JsonElement nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(nameElement.GetString())
            || !element.TryGetProperty("terms", out JsonElement termsElement)
            || termsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<ServiceTermView> terms = [];
        foreach (JsonElement term in termsElement.EnumerateArray())
        {
            if (!TryMapServiceTerm(term, out ServiceTermView? mapped) || mapped is null)
            {
                return false;
            }

            terms.Add(mapped);
        }

        string? description = null;
        if (element.TryGetProperty("description", out JsonElement descriptionElement)
            && descriptionElement.ValueKind == JsonValueKind.String)
        {
            description = descriptionElement.GetString();
        }

        view = new ServiceObjectView
        {
            Id = id,
            Name = nameElement.GetString()!,
            Terms = terms,
            Description = description,
        };
        return true;
    }

    private static bool TryMapServiceTerm(JsonElement element, out ServiceTermView? view)
    {
        view = null;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("protocol", out JsonElement protocolElement)
            || !TryMapProtocol(protocolElement, out IpProtocolView? protocol)
            || protocol is null)
        {
            return false;
        }

        view = new ServiceTermView
        {
            Protocol = protocol,
            SourcePorts = MapPorts(element, "source_ports"),
            DestinationPorts = MapPorts(element, "destination_ports"),
            IcmpSelectors = MapIcmp(element, "icmp_selectors"),
        };
        return true;
    }

    private static bool TryMapProtocol(JsonElement element, out IpProtocolView? view)
    {
        view = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (element.TryGetProperty("any", out JsonElement anyElement) && anyElement.ValueKind == JsonValueKind.True)
        {
            view = new IpProtocolView { Any = true };
            return true;
        }

        if (!element.TryGetProperty("number", out JsonElement numberElement)
            || numberElement.ValueKind != JsonValueKind.Number
            || !numberElement.TryGetByte(out byte number))
        {
            return false;
        }

        string? name = null;
        if (element.TryGetProperty("canonical_name", out JsonElement nameElement)
            && nameElement.ValueKind == JsonValueKind.String)
        {
            name = nameElement.GetString();
        }

        view = new IpProtocolView { Any = false, Number = number, CanonicalName = name };
        return true;
    }

    private static List<PortIntervalView> MapPorts(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<PortIntervalView> ports = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            ports.Add(new PortIntervalView
            {
                Start = item.GetProperty("start").GetUInt16(),
                End = item.GetProperty("end").GetUInt16(),
            });
        }

        return ports;
    }

    private static List<IcmpSelectorView> MapIcmp(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<IcmpSelectorView> items = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            byte type = item.GetProperty("type").GetByte();
            byte? code = null;
            if (item.TryGetProperty("code", out JsonElement codeElement) && codeElement.ValueKind == JsonValueKind.Number)
            {
                code = codeElement.GetByte();
            }

            items.Add(new IcmpSelectorView { Type = type, Code = code });
        }

        return items;
    }

    private static bool TryReadId(JsonElement element, out Guid id)
    {
        id = default;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty("id", out JsonElement idElement)
               && idElement.ValueKind == JsonValueKind.String
               && Guid.TryParse(idElement.GetString(), out id);
    }

    private static bool TryParseFamily(string? text, out IpAddressFamily family)
    {
        switch (text)
        {
            case "IPv4":
                family = IpAddressFamily.IPv4;
                return true;
            case "IPv6":
                family = IpAddressFamily.IPv6;
                return true;
            default:
                family = default;
                return false;
        }
    }

    public static AddressEntry ToAddressEntry(AddressObjectEntryView entry, IpAddressFamily family)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Kind.ToUpperInvariant() switch
        {
            "HOST" => AddressEntry.Host(family, ParseIp(entry.Address, family, "address")),
            "PREFIX" => AddressEntry.Prefix(
                family,
                ParseIp(entry.Address, family, "address"),
                entry.PrefixLength
                ?? throw new Domain.DomainInvariantException("PREFIX entry requires prefix_length.")),
            "RANGE" => AddressEntry.Range(
                family,
                ParseIp(entry.Start, family, "start"),
                ParseIp(entry.End, family, "end")),
            _ => throw new Domain.DomainInvariantException($"Unknown address entry kind '{entry.Kind}'."),
        };
    }

    public static ServiceTerm ToServiceTerm(ServiceTermView term)
    {
        ArgumentNullException.ThrowIfNull(term);
        IpProtocol protocol = term.Protocol.Any
            ? IpProtocol.Any
            : IpProtocol.Create(
                term.Protocol.Number
                ?? throw new Domain.DomainInvariantException("Service term protocol number is required."),
                term.Protocol.CanonicalName);
        PortSet? source = term.SourcePorts.Count == 0
            ? null
            : PortSet.Create(term.SourcePorts.Select(static p => new PortInterval(p.Start, p.End)));
        PortSet? destination = term.DestinationPorts.Count == 0
            ? null
            : PortSet.Create(term.DestinationPorts.Select(static p => new PortInterval(p.Start, p.End)));
        IcmpSelectorSet? icmp = term.IcmpSelectors.Count == 0
            ? null
            : IcmpSelectorSet.Create(term.IcmpSelectors.Select(static i => new IcmpSelector(i.Type, i.Code)));
        return ServiceTerm.Create(protocol, source, destination, icmp);
    }

    private static IPAddress ParseIp(string? text, IpAddressFamily family, string field)
    {
        if (string.IsNullOrWhiteSpace(text) || !IPAddress.TryParse(text, out IPAddress? address))
        {
            throw new Domain.DomainInvariantException($"{field} must be a valid IP address.");
        }

        _ = AddressInterval.ToNumeric(address, family);
        return address;
    }
}
