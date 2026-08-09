using System.Text.Json;

namespace Mfc.Domain.Canonicalization;

/// <summary>
/// Parses <c>mfc.canonical-section/1</c> UTF-8 documents into <see cref="CanonicalSection"/>.
/// </summary>
public static class CanonicalSectionParser
{
    /// <summary>
    /// Tries to parse a canonical section document. Returns false for malformed or unsupported payloads.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out CanonicalSection? section)
    {
        section = null;
        if (utf8.IsEmpty)
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(utf8.ToArray());
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("schema", out JsonElement schemaEl)
                || schemaEl.ValueKind != JsonValueKind.String
                || !string.Equals(schemaEl.GetString(), CanonicalSection.Schema, StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetProperty("domain", out JsonElement domainEl)
                || domainEl.ValueKind != JsonValueKind.String
                || !TryParseDomain(domainEl.GetString(), out CanonicalDomain domain))
            {
                return false;
            }

            if (!root.TryGetProperty("section", out JsonElement sectionEl)
                || sectionEl.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(sectionEl.GetString()))
            {
                return false;
            }

            string sectionId = sectionEl.GetString()!.Trim();

            if (!root.TryGetProperty("ordered", out JsonElement orderedEl)
                || (orderedEl.ValueKind is not JsonValueKind.True and not JsonValueKind.False))
            {
                return false;
            }

            bool ordered = orderedEl.GetBoolean();

            if (!root.TryGetProperty("records", out JsonElement recordsEl)
                || recordsEl.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            List<CanonicalRecord> records = [];
            foreach (JsonElement recordEl in recordsEl.EnumerateArray())
            {
                if (recordEl.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                Dictionary<string, string> props = new(StringComparer.Ordinal);
                foreach (JsonProperty property in recordEl.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        // Canonical section records are string-valued property maps.
                        return false;
                    }

                    props[property.Name] = property.Value.GetString() ?? string.Empty;
                }

                records.Add(new CanonicalRecord(props));
            }

            section = new CanonicalSection(domain, sectionId, ordered, records);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryParseDomain(string? value, out CanonicalDomain domain)
    {
        if (string.Equals(value, "configuration", StringComparison.Ordinal))
        {
            domain = CanonicalDomain.Configuration;
            return true;
        }

        if (string.Equals(value, "observations", StringComparison.Ordinal))
        {
            domain = CanonicalDomain.Observations;
            return true;
        }

        domain = default;
        return false;
    }
}

/// <summary>Partial surface for <see cref="CanonicalSection.TryParse"/>.</summary>
public sealed partial class CanonicalSection
{
    /// <summary>Tries to parse UTF-8 canonical section bytes.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out CanonicalSection? section)
        => CanonicalSectionParser.TryParse(utf8, out section);
}
