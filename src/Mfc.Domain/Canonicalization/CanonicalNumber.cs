using System.Globalization;

namespace Mfc.Domain.Canonicalization;

/// <summary>Invariant-culture canonical number serialization (M1-21 AC#5).</summary>
public static class CanonicalNumber
{
    /// <summary>Formats a signed 64-bit integer as decimal without leading zeros (except zero itself).</summary>
    public static string FormatInt64(long value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats an unsigned 64-bit integer as decimal without leading zeros.</summary>
    public static string FormatUInt64(ulong value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses and re-formats a decimal integer string via invariant culture.</summary>
    public static bool TryNormalizeInteger(string? value, out string canonical, out string? error)
    {
        canonical = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Integer value is empty.";
            return false;
        }

        string trimmed = value.Trim();
        if (!long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed))
        {
            error = "Integer value is not a valid invariant decimal.";
            return false;
        }

        canonical = FormatInt64(parsed);
        return true;
    }
}
