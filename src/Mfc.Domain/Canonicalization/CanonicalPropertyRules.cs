namespace Mfc.Domain.Canonicalization;

/// <summary>
/// Property inclusion rules for canonical configuration material (M1-21 AC#7–8).
/// </summary>
public static class CanonicalPropertyRules
{
    private static readonly HashSet<string> CounterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bytes",
        "packets",
        "pcnt",
        "bcnt",
        "byte",
        "packet",
        "rx-byte",
        "tx-byte",
        "rx-packet",
        "tx-packet",
        "rx-bytes",
        "tx-bytes",
        "rx-packets",
        "tx-packets",
    };

    /// <summary>True when the property must never enter canonical configuration.</summary>
    public static bool IsExcludedFromConfiguration(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        string name = propertyName.StartsWith('=') ? propertyName[1..] : propertyName;
        if (string.Equals(name, ".id", StringComparison.Ordinal)
            || string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return CounterNames.Contains(name);
    }

    /// <summary>
    /// Normalizes empty/default representation: null/whitespace-only absent values are omitted;
    /// empty string is preserved when present (Vertical Slice §17.1 #6–7).
    /// </summary>
    public static bool ShouldOmitValue(string? value)
        => value is null;
}
