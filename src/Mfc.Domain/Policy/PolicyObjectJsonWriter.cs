using System.Globalization;
using System.Text.Json;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Serializes typed address/service objects to the opaque JSON shape consumed by
/// <see cref="PolicyObjectJsonReader"/> (Policy Model §16 / §18).
/// </summary>
public static class PolicyObjectJsonWriter
{
    /// <summary>Writes an address object as compose-catalog JSON.</summary>
    public static JsonElement WriteAddress(AddressObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", value.Id.Value.ToString("D"));
            writer.WriteString("name", value.Name.Value);
            writer.WriteString("family", FormatFamily(value.Family));
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            foreach (AddressInterval interval in value.Intervals)
            {
                foreach (Action<Utf8JsonWriter> writeEntry in EnumerateAddressEntries(interval))
                {
                    writeEntry(writer);
                }
            }

            writer.WriteEndArray();
            if (!string.IsNullOrWhiteSpace(value.Description))
            {
                writer.WriteString("description", value.Description);
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    /// <summary>Writes a service object as compose-catalog JSON.</summary>
    public static JsonElement WriteService(ServiceObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", value.Id.Value.ToString("D"));
            writer.WriteString("name", value.Name.Value);
            writer.WritePropertyName("terms");
            writer.WriteStartArray();
            foreach (ServiceTerm term in value.Terms)
            {
                WriteServiceTerm(writer, term);
            }

            writer.WriteEndArray();
            if (!string.IsNullOrWhiteSpace(value.Description))
            {
                writer.WriteString("description", value.Description);
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    private static IEnumerable<Action<Utf8JsonWriter>> EnumerateAddressEntries(AddressInterval interval)
    {
        if (interval.Start == interval.End)
        {
            yield return writer => WriteHost(writer, interval.Family, interval.Start);
            yield break;
        }

        if (interval.Family == IpAddressFamily.IPv4)
        {
            yield return writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "RANGE");
                writer.WriteString("start", FormatIp(interval.Family, interval.Start));
                writer.WriteString("end", FormatIp(interval.Family, interval.End));
                writer.WriteEndObject();
            };
            yield break;
        }

        if (TryMatchPrefix(interval, out byte prefixLength))
        {
            yield return writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "PREFIX");
                writer.WriteString("address", FormatIp(interval.Family, interval.Start));
                writer.WriteNumber("prefix_length", prefixLength);
                writer.WriteEndObject();
            };
            yield break;
        }

        // IPv6 RANGE is forbidden. Two adjacent hosts can be emitted as HOST endpoints;
        // larger non-aligned spans cannot be represented without inventing RANGE.
        if (interval.End == interval.Start + 1)
        {
            yield return writer => WriteHost(writer, interval.Family, interval.Start);
            yield return writer => WriteHost(writer, interval.Family, interval.End);
            yield break;
        }

        throw new DomainInvariantException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"IPv6 address interval {FormatIp(interval.Family, interval.Start)}-" +
                $"{FormatIp(interval.Family, interval.End)} is not a CIDR-aligned PREFIX and cannot be serialized."));
    }

    private static void WriteHost(Utf8JsonWriter writer, IpAddressFamily family, UInt128 address)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", "HOST");
        writer.WriteString("address", FormatIp(family, address));
        writer.WriteEndObject();
    }

    private static bool TryMatchPrefix(AddressInterval interval, out byte prefixLength)
    {
        prefixLength = 0;
        int width = interval.Family == IpAddressFamily.IPv4 ? 32 : 128;
        for (int length = 0; length <= width; length++)
        {
            AddressInterval candidate = AddressInterval.FromPrefix(
                interval.Family,
                AddressInterval.FromNumeric(interval.Family, interval.Start),
                length);
            if (candidate.Start == interval.Start && candidate.End == interval.End)
            {
                prefixLength = (byte)length;
                return true;
            }
        }

        return false;
    }

    private static void WriteServiceTerm(Utf8JsonWriter writer, ServiceTerm term)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("protocol");
        WriteProtocol(writer, term.Protocol);
        if (term.SourcePorts is not null)
        {
            writer.WritePropertyName("source_ports");
            WritePorts(writer, term.SourcePorts);
        }

        if (term.DestinationPorts is not null)
        {
            writer.WritePropertyName("destination_ports");
            WritePorts(writer, term.DestinationPorts);
        }

        if (term.IcmpSelectors is not null)
        {
            writer.WritePropertyName("icmp_selectors");
            WriteIcmp(writer, term.IcmpSelectors);
        }

        writer.WriteEndObject();
    }

    private static void WriteProtocol(Utf8JsonWriter writer, IpProtocol protocol)
    {
        writer.WriteStartObject();
        if (protocol.IsAny)
        {
            writer.WriteBoolean("any", true);
        }
        else
        {
            writer.WriteNumber("number", protocol.Number);
            if (!string.IsNullOrWhiteSpace(protocol.CanonicalName))
            {
                writer.WriteString("canonical_name", protocol.CanonicalName);
            }
        }

        writer.WriteEndObject();
    }

    private static void WritePorts(Utf8JsonWriter writer, PortSet ports)
    {
        writer.WriteStartArray();
        foreach (PortInterval interval in ports.Intervals)
        {
            writer.WriteStartObject();
            writer.WriteNumber("start", interval.Start);
            writer.WriteNumber("end", interval.End);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteIcmp(Utf8JsonWriter writer, IcmpSelectorSet icmp)
    {
        writer.WriteStartArray();
        foreach (IcmpSelector selector in icmp.Items)
        {
            writer.WriteStartObject();
            writer.WriteNumber("type", selector.Type);
            if (selector.Code is byte code)
            {
                writer.WriteNumber("code", code);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string FormatFamily(IpAddressFamily family)
        => family switch
        {
            IpAddressFamily.IPv4 => "IPv4",
            IpAddressFamily.IPv6 => "IPv6",
            _ => throw new DomainInvariantException($"Unsupported address family '{family}'."),
        };

    private static string FormatIp(IpAddressFamily family, UInt128 value)
        => AddressInterval.FromNumeric(family, value).ToString();
}
