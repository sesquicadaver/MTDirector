using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Routing;

/// <summary>Deterministic CIDR / address matching for route resolution.</summary>
internal static class RouteResolutionPrefixMatcher
{
    internal static bool TryParseFamily(string family, out IpAddressFamily parsed)
    {
        parsed = IpAddressFamily.IPv4;
        if (string.IsNullOrWhiteSpace(family))
        {
            return false;
        }

        switch (family.Trim().ToLowerInvariant())
        {
            case "ipv4" or "4":
                parsed = IpAddressFamily.IPv4;
                return true;
            case "ipv6" or "6":
                parsed = IpAddressFamily.IPv6;
                return true;
            default:
                return false;
        }
    }

    internal static bool TryParseAddress(string family, string? value, out UInt128 numeric)
    {
        numeric = UInt128.Zero;
        if (!TryParseFamily(family, out IpAddressFamily parsed) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value.Trim(), out IPAddress? address) || address is null)
        {
            return false;
        }

        numeric = ToNumeric(address, parsed);
        return true;
    }

    internal static bool TryParsePrefix(string family, string? prefix, out UInt128 network, out int prefixLength, out UInt128 end)
    {
        network = UInt128.Zero;
        prefixLength = 0;
        end = UInt128.Zero;
        if (!TryParseFamily(family, out IpAddressFamily parsed) || string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        string trimmed = prefix.Trim();
        int slash = trimmed.LastIndexOf('/');
        if (slash <= 0)
        {
            if (!IPAddress.TryParse(trimmed, out IPAddress? host) || host is null)
            {
                return false;
            }

            int width = parsed == IpAddressFamily.IPv4 ? 32 : 128;
            network = ToNumeric(host, parsed);
            prefixLength = width;
            end = network;
            return true;
        }

        string addressPart = trimmed[..slash];
        if (!IPAddress.TryParse(addressPart, out IPAddress? address) || address is null)
        {
            return false;
        }

        if (!int.TryParse(trimmed[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out prefixLength))
        {
            return false;
        }

        int max = parsed == IpAddressFamily.IPv4 ? 32 : 128;
        if (prefixLength < 0 || prefixLength > max)
        {
            return false;
        }

        network = MaskHostBits(parsed, ToNumeric(address, parsed), prefixLength);
        if (prefixLength == max)
        {
            end = network;
        }
        else if (prefixLength == 0)
        {
            end = parsed == IpAddressFamily.IPv4 ? uint.MaxValue : UInt128.MaxValue;
        }
        else
        {
            int hostBits = max - prefixLength;
            UInt128 hostMask = (UInt128.One << hostBits) - 1;
            end = network | hostMask;
        }

        return true;
    }

    internal static bool Contains(string family, string? prefix, UInt128 address)
        => TryParsePrefix(family, prefix, out UInt128 network, out int prefixLength, out UInt128 end)
           && address >= network
           && address <= end;

    internal static int PrefixLength(string family, string? prefix)
        => TryParsePrefix(family, prefix, out _, out int length, out _) ? length : -1;

    internal static bool MatchesSelector(string family, string? selector, UInt128 address)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return true;
        }

        string trimmed = selector.Trim();
        if (trimmed == "0.0.0.0/0" || trimmed == "::/0")
        {
            return true;
        }

        return Contains(family, trimmed, address);
    }

    internal static UInt128 ToNumeric(IPAddress address, IpAddressFamily expectedFamily)
    {
        IpAddressFamily family = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IpAddressFamily.IPv4,
            AddressFamily.InterNetworkV6 => IpAddressFamily.IPv6,
            _ => throw new DomainInvariantException("Only IPv4/IPv6 addresses are supported."),
        };
        if (family != expectedFamily)
        {
            throw new DomainInvariantException($"Address family mismatch: expected {expectedFamily}, got {family}.");
        }

        byte[] bytes = address.GetAddressBytes();
        if (family == IpAddressFamily.IPv4)
        {
            return ((uint)bytes[0] << 24)
                   | ((uint)bytes[1] << 16)
                   | ((uint)bytes[2] << 8)
                   | bytes[3];
        }

        UInt128 value = UInt128.Zero;
        foreach (byte b in bytes)
        {
            value = (value << 8) | b;
        }

        return value;
    }

    internal static UInt128 MaskHostBits(IpAddressFamily family, UInt128 address, int prefixLength)
    {
        int width = family == IpAddressFamily.IPv4 ? 32 : 128;
        if (prefixLength <= 0)
        {
            return UInt128.Zero;
        }

        if (prefixLength >= width)
        {
            return address;
        }

        int hostBits = width - prefixLength;
        UInt128 hostMask = (UInt128.One << hostBits) - 1;
        return address & ~hostMask;
    }
}
