using System.Globalization;
using System.Net;
using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Parses compose-merged address/service JSON into typed objects (Policy Model §16 / §18).
/// Identity is taken from the compose catalog, not from optional JSON owner fields.
/// </summary>
public static class PolicyObjectJsonReader
{
    public static bool TryReadAddress(
        JsonElement element,
        PolicyObjectIdentity identity,
        out AddressObject? value,
        out string? error)
    {
        value = null;
        error = null;
        ArgumentNullException.ThrowIfNull(identity);
        try
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                error = "Address object JSON must be an object.";
                return false;
            }

            if (!TryReadId(element, identity.Id, "address", out error))
            {
                return false;
            }

            if (!element.TryGetProperty("name", out JsonElement nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                error = $"Address object '{identity.Id:D}' is missing a name.";
                return false;
            }

            if (!element.TryGetProperty("family", out JsonElement familyElement)
                || familyElement.ValueKind != JsonValueKind.String
                || !TryParseFamily(familyElement.GetString(), out IpAddressFamily family))
            {
                error = $"Address object '{identity.Id:D}' is missing a family.";
                return false;
            }

            if (!element.TryGetProperty("entries", out JsonElement entriesElement)
                || entriesElement.ValueKind != JsonValueKind.Array
                || entriesElement.GetArrayLength() == 0)
            {
                error = $"Address object '{identity.Id:D}' has no parseable entries.";
                return false;
            }

            List<AddressInterval> intervals = [];
            foreach (JsonElement entry in entriesElement.EnumerateArray())
            {
                if (!TryReadAddressEntry(entry, family, out AddressInterval interval, out error))
                {
                    return false;
                }

                intervals.Add(interval);
            }

            string? description = null;
            if (element.TryGetProperty("description", out JsonElement descriptionElement)
                && descriptionElement.ValueKind == JsonValueKind.String)
            {
                description = descriptionElement.GetString();
            }

            value = AddressObject.Reconstitute(
                new AddressObjectId(identity.Id),
                identity.OwnerScope,
                identity.OwnerId,
                identity.ExceptionRevisionId,
                NonEmptyName.Create(nameElement.GetString()!),
                family,
                description,
                intervals);
            return true;
        }
        catch (Exception ex) when (ex is DomainInvariantException or JsonException or FormatException or InvalidOperationException)
        {
            error = ex.Message;
            value = null;
            return false;
        }
    }

    public static bool TryReadService(
        JsonElement element,
        PolicyObjectIdentity identity,
        out ServiceObject? value,
        out string? error)
    {
        value = null;
        error = null;
        ArgumentNullException.ThrowIfNull(identity);
        try
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                error = "Service object JSON must be an object.";
                return false;
            }

            if (!TryReadId(element, identity.Id, "service", out error))
            {
                return false;
            }

            if (!element.TryGetProperty("name", out JsonElement nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                error = $"Service object '{identity.Id:D}' is missing a name.";
                return false;
            }

            if (!element.TryGetProperty("terms", out JsonElement termsElement)
                || termsElement.ValueKind != JsonValueKind.Array
                || termsElement.GetArrayLength() == 0)
            {
                error = $"Service object '{identity.Id:D}' has no parseable terms.";
                return false;
            }

            List<ServiceTerm> terms = [];
            foreach (JsonElement term in termsElement.EnumerateArray())
            {
                if (!TryReadServiceTerm(term, out ServiceTerm? parsed, out error) || parsed is null)
                {
                    return false;
                }

                terms.Add(parsed);
            }

            string? description = null;
            if (element.TryGetProperty("description", out JsonElement descriptionElement)
                && descriptionElement.ValueKind == JsonValueKind.String)
            {
                description = descriptionElement.GetString();
            }

            value = ServiceObject.Reconstitute(
                new ServiceObjectId(identity.Id),
                identity.OwnerScope,
                identity.OwnerId,
                identity.ExceptionRevisionId,
                NonEmptyName.Create(nameElement.GetString()!),
                description,
                terms);
            return true;
        }
        catch (Exception ex) when (ex is DomainInvariantException or JsonException or FormatException or InvalidOperationException)
        {
            error = ex.Message;
            value = null;
            return false;
        }
    }

    private static bool TryReadId(JsonElement element, Guid expected, string kind, out string? error)
    {
        error = null;
        if (!element.TryGetProperty("id", out JsonElement idElement)
            || idElement.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idElement.GetString(), out Guid id)
            || id != expected)
        {
            error = $"{kind} object id does not match the compose catalog identity '{expected:D}'.";
            return false;
        }

        return true;
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

    private static bool TryReadAddressEntry(
        JsonElement element,
        IpAddressFamily family,
        out AddressInterval interval,
        out string? error)
    {
        interval = default;
        error = null;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("kind", out JsonElement kindElement)
            || kindElement.ValueKind != JsonValueKind.String)
        {
            error = "Address entry must be an object with kind.";
            return false;
        }

        string? kind = kindElement.GetString();
        try
        {
            interval = kind switch
            {
                "HOST" => AddressEntry.Host(family, ReadIp(element, "address", family)).ToInterval(),
                "PREFIX" => AddressEntry.Prefix(
                    family,
                    ReadIp(element, "address", family),
                    ReadByte(element, "prefix_length")).ToInterval(),
                "RANGE" => AddressEntry.Range(
                    family,
                    ReadIp(element, "start", family),
                    ReadIp(element, "end", family)).ToInterval(),
                _ => throw new DomainInvariantException(
                    string.Create(CultureInfo.InvariantCulture, $"Unknown address entry kind '{kind}'.")),
            };
            return true;
        }
        catch (Exception ex) when (ex is DomainInvariantException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadServiceTerm(JsonElement element, out ServiceTerm? term, out string? error)
    {
        term = null;
        error = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "Service term must be a JSON object.";
            return false;
        }

        try
        {
            if (!element.TryGetProperty("protocol", out JsonElement protocolElement)
                || !TryReadProtocol(protocolElement, out IpProtocol? protocol)
                || protocol is null)
            {
                error = "Service term protocol is missing or invalid.";
                return false;
            }

            PortSet? source = TryReadPorts(element, "source_ports");
            PortSet? destination = TryReadPorts(element, "destination_ports");
            IcmpSelectorSet? icmp = TryReadIcmp(element, "icmp_selectors");
            term = ServiceTerm.Create(protocol, source, destination, icmp);
            return true;
        }
        catch (Exception ex) when (ex is DomainInvariantException or InvalidOperationException or FormatException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadProtocol(JsonElement element, out IpProtocol? protocol)
    {
        protocol = null;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("any", out JsonElement anyElement)
                && anyElement.ValueKind == JsonValueKind.True)
            {
                protocol = IpProtocol.Any;
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

            protocol = IpProtocol.Create(number, name);
            return true;
        }

        return false;
    }

    private static PortSet? TryReadPorts(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<PortInterval> intervals = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            ushort start = ReadUInt16(item, "start");
            ushort end = ReadUInt16(item, "end");
            intervals.Add(new PortInterval(start, end));
        }

        return intervals.Count == 0 ? null : PortSet.Create(intervals);
    }

    private static IcmpSelectorSet? TryReadIcmp(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<IcmpSelector> items = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            byte type = ReadByte(item, "type");
            byte? code = null;
            if (item.TryGetProperty("code", out JsonElement codeElement) && codeElement.ValueKind == JsonValueKind.Number)
            {
                code = ReadByte(item, "code");
            }

            items.Add(new IcmpSelector(type, code));
        }

        return items.Count == 0 ? null : IcmpSelectorSet.Create(items);
    }

    private static IPAddress ReadIp(JsonElement parent, string name, IpAddressFamily family)
    {
        string text = parent.GetProperty(name).GetString()
                      ?? throw new DomainInvariantException($"{name} is required.");
        if (!IPAddress.TryParse(text, out IPAddress? address))
        {
            throw new DomainInvariantException($"Invalid IP address '{text}'.");
        }

        _ = AddressInterval.ToNumeric(address, family);
        return address;
    }

    private static byte ReadByte(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetByte(out byte value))
        {
            throw new DomainInvariantException($"{name} must be an unsigned byte.");
        }

        return value;
    }

    private static ushort ReadUInt16(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt16(out ushort value))
        {
            throw new DomainInvariantException($"{name} must be an unsigned 16-bit integer.");
        }

        return value;
    }
}
