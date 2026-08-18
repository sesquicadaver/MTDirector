using System.Globalization;
using System.Text;

namespace Mfc.Domain.Policy;

/// <summary>
/// Deterministic RouterOS <c>src-port</c> / <c>dst-port</c> encoding (Compiler Spec §19.2 / §27).
/// Does not split oversized matchers.
/// </summary>
public static class PortMatcherEncoder
{
    public static string Encode(PortSet ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (ports.Intervals.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        for (int i = 0; i < ports.Intervals.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            PortInterval interval = ports.Intervals[i];
            if (interval.Start == interval.End)
            {
                builder.Append(interval.Start.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(CultureInfo.InvariantCulture, $"{interval.Start}-{interval.End}");
            }
        }

        return builder.ToString();
    }

    public static int Utf8ByteCount(string encoded)
        => Encoding.UTF8.GetByteCount(encoded);
}
