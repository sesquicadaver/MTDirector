namespace Mfc.Domain.Canonicalization;

/// <summary>
/// Input record prior to canonicalization (raw property bags from discovery / raw snapshot).
/// </summary>
public sealed class CanonicalRecordInput
{
    public required IReadOnlyDictionary<string, string> Properties { get; init; }
}

/// <summary>Input section for the shared canonicalization mechanism (M1-21).</summary>
public sealed class CanonicalSectionInput
{
    public required CanonicalDomain Domain { get; init; }

    public required string SectionId { get; init; }

    public required bool Ordered { get; init; }

    public required IReadOnlyList<CanonicalRecordInput> Records { get; init; }
}

/// <summary>
/// Single normalization entry point: filters .id/counters, normalizes values, emits deterministic bytes.
/// Idempotent: <c>Canonicalize(Canonicalize(x)) == Canonicalize(x)</c> (M1-21 AC#11–12).
/// </summary>
public static class Canonicalizer
{
    /// <summary>Canonicalizes a section input into a <see cref="CanonicalSection"/>.</summary>
    public static CanonicalSection Canonicalize(CanonicalSectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SectionId);
        ArgumentNullException.ThrowIfNull(input.Records);

        List<CanonicalRecord> records = new(input.Records.Count);
        foreach (CanonicalRecordInput record in input.Records)
        {
            ArgumentNullException.ThrowIfNull(record);
            records.Add(new CanonicalRecord(NormalizeProperties(record.Properties, input.Domain)));
        }

        if (!input.Ordered)
        {
            records = records
                .OrderBy(static r => CanonicalCollections.StableSortKey(r.Properties), StringComparer.Ordinal)
                .ToList();
        }

        return new CanonicalSection(input.Domain, input.SectionId.Trim(), input.Ordered, records);
    }

    /// <summary>
    /// Re-canonicalizes an already canonical section (idempotence helper for tests and pipelines).
    /// </summary>
    public static CanonicalSection Canonicalize(CanonicalSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        CanonicalSectionInput input = new()
        {
            Domain = section.Domain,
            SectionId = section.SectionId,
            Ordered = section.Ordered,
            Records = section.Records.Select(static r => new CanonicalRecordInput
            {
                Properties = r.Properties,
            }).ToArray(),
        };
        return Canonicalize(input);
    }

    private static SortedDictionary<string, string> NormalizeProperties(
        IReadOnlyDictionary<string, string> properties,
        CanonicalDomain domain)
    {
        ArgumentNullException.ThrowIfNull(properties);
        SortedDictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach ((string key, string value) in properties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string name = key.StartsWith('=') ? key[1..] : key;
            if (domain == CanonicalDomain.Configuration
                && CanonicalPropertyRules.IsExcludedFromConfiguration(name))
            {
                continue;
            }

            if (CanonicalPropertyRules.ShouldOmitValue(value))
            {
                continue;
            }

            string canonicalValue = NormalizeValue(name, value);
            normalized[name] = canonicalValue;
        }

        return normalized;
    }

    private static string NormalizeValue(string name, string value)
    {
        // Empty string is preserved (has semantics).
        if (value.Length == 0)
        {
            return value;
        }

        if (string.Equals(name, "disabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "dynamic", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || value == "1")
            {
                return "true";
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
                || value == "0")
            {
                return "false";
            }
        }

        if (CanonicalNumber.TryNormalizeInteger(value, out string integer, out _))
        {
            // Only rewrite when the property looks numeric (distance, port, vrid, etc.).
            if (LooksNumericProperty(name))
            {
                return integer;
            }
        }

        if (value.Contains('/', StringComparison.Ordinal)
            && CanonicalIp.TryCanonicalizeInterfaceAddress(value, out string ifaddr, out _))
        {
            return ifaddr;
        }

        if (CanonicalIp.TryCanonicalizeAddress(value, out string ip, out _))
        {
            return ip;
        }

        return value;
    }

    private static bool LooksNumericProperty(string name)
        => name.Contains("port", StringComparison.OrdinalIgnoreCase)
           || name.Contains("distance", StringComparison.OrdinalIgnoreCase)
           || name.Contains("vrid", StringComparison.OrdinalIgnoreCase)
           || name.Contains("priority", StringComparison.OrdinalIgnoreCase)
           || name.Equals("mtu", StringComparison.OrdinalIgnoreCase)
           || name.Equals("scope", StringComparison.OrdinalIgnoreCase);
}
