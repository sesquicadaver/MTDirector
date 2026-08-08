using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// Typed management host: IPv4, IPv6, or DNS hostname — never an opaque free-form string.
/// </summary>
public sealed partial class HostNameOrIp : IEquatable<HostNameOrIp>
{
    private static readonly Regex DnsLabel = DnsLabelRegex();

    public enum Kind : byte
    {
        IPv4 = 0,
        IPv6 = 1,
        DnsHostName = 2,
    }

    public Kind HostKind { get; }

    /// <summary>Canonical textual form (IP or hostname).</summary>
    public string Value { get; }

    public IPAddress? Address { get; }

    private HostNameOrIp(Kind hostKind, string value, IPAddress? address)
    {
        HostKind = hostKind;
        Value = value;
        Address = address;
    }

    public static HostNameOrIp Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();

        if (IPAddress.TryParse(trimmed, out IPAddress? ip))
        {
            return ip.AddressFamily switch
            {
                AddressFamily.InterNetwork => new HostNameOrIp(Kind.IPv4, ip.ToString(), ip),
                AddressFamily.InterNetworkV6 => new HostNameOrIp(Kind.IPv6, ip.ToString(), ip),
                _ => throw new DomainInvariantException("Unsupported IP address family."),
            };
        }

        string host = trimmed.EndsWith('.') ? trimmed[..^1] : trimmed;
        if (!IsValidDnsHostName(host))
        {
            throw new DomainInvariantException(
                "management_host must be a valid IPv4/IPv6 address or DNS hostname.");
        }

        return new HostNameOrIp(Kind.DnsHostName, host.ToLowerInvariant(), address: null);
    }

    public bool Equals(HostNameOrIp? other)
        => other is not null
           && HostKind == other.HostKind
           && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is HostNameOrIp other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(HostKind, Value);

    public override string ToString() => Value;

    private static bool IsValidDnsHostName(string value)
    {
        if (value.Length is < 1 or > 253)
        {
            return false;
        }

        string[] labels = value.Split('.');
        foreach (string label in labels)
        {
            if (!DnsLabel.IsMatch(label))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex("^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex DnsLabelRegex();
}
