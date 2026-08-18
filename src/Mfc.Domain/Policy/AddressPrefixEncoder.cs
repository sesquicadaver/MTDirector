using System.Globalization;
using System.Net;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Deterministic CIDR covering of disjoint address intervals for RouterOS address-list entries.
/// Hosts omit prefix length; non-host prefixes use <c>addr/len</c>. No timeout tokens.
/// </summary>
public static class AddressPrefixEncoder
{
    /// <summary>Encodes already-normalized disjoint intervals as sorted unique RouterOS address tokens.</summary>
    public static IReadOnlyList<string> Encode(IReadOnlyList<AddressInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        if (intervals.Count == 0)
        {
            return [];
        }

        List<string> encoded = [];
        foreach (AddressInterval interval in intervals)
        {
            EncodeInterval(interval, encoded);
        }

        encoded.Sort(StringComparer.Ordinal);
        return encoded;
    }

    private static void EncodeInterval(AddressInterval interval, List<string> encoded)
    {
        int width = Width(interval.Family);
        if (interval.Start == UInt128.Zero && interval.End == AddressInterval.MaxValue(interval.Family))
        {
            encoded.Add(FormatPrefix(interval.Family, UInt128.Zero, prefixLength: 0));
            return;
        }

        UInt128 cursor = interval.Start;
        UInt128 end = interval.End;
        while (cursor <= end)
        {
            int align = TrailingZeroCount(cursor, width);
            UInt128 remainingInclusive = end - cursor;
            int maxSize = remainingInclusive == UInt128.MaxValue
                ? width
                : FloorLog2(remainingInclusive + 1);
            int size = Math.Min(align, maxSize);
            int prefixLength = width - size;
            encoded.Add(FormatPrefix(interval.Family, cursor, prefixLength));
            if (size == width)
            {
                break;
            }

            UInt128 step = UInt128.One << size;
            if (cursor > UInt128.MaxValue - (step - 1) || cursor + (step - 1) >= end)
            {
                break;
            }

            cursor += step;
        }
    }

    private static string FormatPrefix(IpAddressFamily family, UInt128 network, int prefixLength)
    {
        IPAddress address = AddressInterval.FromNumeric(family, network);
        string host = address.ToString().ToLowerInvariant();
        int width = Width(family);
        if (prefixLength == width)
        {
            return host;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{host}/{prefixLength}");
    }

    private static int Width(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? 32 : 128;

    private static int TrailingZeroCount(UInt128 value, int width)
    {
        if (value == UInt128.Zero)
        {
            return width;
        }

        int count = 0;
        while ((value & 1) == 0 && count < width)
        {
            value >>= 1;
            count++;
        }

        return count;
    }

    private static int FloorLog2(UInt128 value)
    {
        if (value == 0)
        {
            throw new DomainInvariantException("FloorLog2 requires a positive value.");
        }

        int bits = 0;
        while (value > 1)
        {
            value >>= 1;
            bits++;
        }

        return bits;
    }
}
