using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Mfc.RouterOs.Discovery;

/// <summary>Normalizes RouterOS CIDR address strings for deterministic discovery (M1-12).</summary>
public static class CidrNormalizer
{
    /// <summary>Normalizes an IPv4 CIDR (<c>a.b.c.d/prefix</c>) to canonical decimal form.</summary>
    public static bool TryNormalizeIpv4(string? value, out string normalized, out string? error)
        => TryNormalize(value, AddressFamily.InterNetwork, maxPrefix: 32, out normalized, out error);

    /// <summary>Normalizes an IPv6 CIDR to compressed lowercase form with prefix.</summary>
    public static bool TryNormalizeIpv6(string? value, out string normalized, out string? error)
        => TryNormalize(value, AddressFamily.InterNetworkV6, maxPrefix: 128, out normalized, out error);

    private static bool TryNormalize(
        string? value,
        AddressFamily family,
        int maxPrefix,
        out string normalized,
        out string? error)
    {
        normalized = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "CIDR value is empty.";
            return false;
        }

        string trimmed = value.Trim();
        int slash = trimmed.LastIndexOf('/');
        if (slash <= 0 || slash == trimmed.Length - 1)
        {
            error = "CIDR must include a prefix length.";
            return false;
        }

        string addressPart = trimmed[..slash];
        string prefixPart = trimmed[(slash + 1)..];
        if (!IPAddress.TryParse(addressPart, out IPAddress? address) || address is null)
        {
            error = "CIDR address is not a valid IP address.";
            return false;
        }

        if (address.AddressFamily != family)
        {
            error = family == AddressFamily.InterNetwork
                ? "Expected IPv4 CIDR."
                : "Expected IPv6 CIDR.";
            return false;
        }

        if (!int.TryParse(prefixPart, NumberStyles.None, CultureInfo.InvariantCulture, out int prefix)
            || prefix < 0
            || prefix > maxPrefix)
        {
            error = $"CIDR prefix must be an integer in 0..{maxPrefix}.";
            return false;
        }

        // Reject ambiguous IPv4 octal/leading-zero forms (e.g. 010.0.0.1 → 8.0.0.1).
        if (family == AddressFamily.InterNetwork
            && !string.Equals(address.ToString(), addressPart, StringComparison.Ordinal))
        {
            error = "IPv4 CIDR must use canonical decimal octets without leading zeros.";
            return false;
        }

        // IPv6 ToString is compressed lowercase; IPv4 is dotted-decimal.
        normalized = string.Concat(
            address.ToString(),
            "/",
            prefix.ToString(CultureInfo.InvariantCulture));
        return true;
    }
}
