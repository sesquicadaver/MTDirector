using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Mfc.Domain.Canonicalization;

/// <summary>
/// Canonical IP / prefix / interface-address forms (Vertical Slice §17.2, M1-21 AC#1).
/// </summary>
public static class CanonicalIp
{
    /// <summary>Canonical dotted-decimal IPv4 or compressed lowercase IPv6 host address.</summary>
    public static bool TryCanonicalizeAddress(string? value, out string canonical, out string? error)
    {
        canonical = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "IP address is empty.";
            return false;
        }

        string trimmed = value.Trim();
        if (!IPAddress.TryParse(trimmed, out IPAddress? address) || address is null)
        {
            error = "IP address is invalid.";
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork
            && !string.Equals(address.ToString(), trimmed, StringComparison.Ordinal))
        {
            error = "IPv4 address must use canonical decimal octets without leading zeros.";
            return false;
        }

        canonical = address.ToString();
        return true;
    }

    /// <summary>
    /// Interface address with prefix: host bits preserved (e.g. 192.168.1.19/24).
    /// </summary>
    public static bool TryCanonicalizeInterfaceAddress(string? value, out string canonical, out string? error)
        => TryCanonicalizeCidr(value, maskHostBits: false, out canonical, out error);

    /// <summary>
    /// Network prefix: host bits masked (e.g. 192.168.1.19/24 → 192.168.1.0/24).
    /// </summary>
    public static bool TryCanonicalizePrefix(string? value, out string canonical, out string? error)
        => TryCanonicalizeCidr(value, maskHostBits: true, out canonical, out error);

    private static bool TryCanonicalizeCidr(
        string? value,
        bool maskHostBits,
        out string canonical,
        out string? error)
    {
        canonical = string.Empty;
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

        int maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (!int.TryParse(prefixPart, NumberStyles.None, CultureInfo.InvariantCulture, out int prefix)
            || prefix < 0
            || prefix > maxPrefix)
        {
            error = $"CIDR prefix must be an integer in 0..{maxPrefix}.";
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork
            && !string.Equals(address.ToString(), addressPart, StringComparison.Ordinal))
        {
            error = "IPv4 CIDR must use canonical decimal octets without leading zeros.";
            return false;
        }

        IPAddress networkOrHost = maskHostBits ? MaskHostBits(address, prefix) : address;
        canonical = string.Concat(
            networkOrHost.ToString(),
            "/",
            prefix.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    private static IPAddress MaskHostBits(IPAddress address, int prefixLength)
    {
        byte[] bytes = address.GetAddressBytes();
        int totalBits = bytes.Length * 8;
        if (prefixLength >= totalBits)
        {
            return address;
        }

        if (bytes.Length == 4)
        {
            uint value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            uint mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
            uint network = value & mask;
            Span<byte> result = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(result, network);
            return new IPAddress(result);
        }

        Span<byte> copy = stackalloc byte[16];
        bytes.CopyTo(copy);
        for (int bit = prefixLength; bit < 128; bit++)
        {
            int byteIndex = bit / 8;
            int bitIndex = 7 - (bit % 8);
            copy[byteIndex] = (byte)(copy[byteIndex] & ~(1 << bitIndex));
        }

        return new IPAddress(copy);
    }
}
