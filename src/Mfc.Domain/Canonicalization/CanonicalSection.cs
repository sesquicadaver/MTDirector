namespace Mfc.Domain.Canonicalization;

/// <summary>Canonical domain for a section document.</summary>
public enum CanonicalDomain : byte
{
    Configuration = 0,
    Observations = 1,
}

/// <summary>One record after property filtering and value normalization.</summary>
public sealed class CanonicalRecord
{
    public CanonicalRecord(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Properties = properties;
    }

    public IReadOnlyDictionary<string, string> Properties { get; }
}

/// <summary>
/// Canonical section document (<c>mfc.canonical-section/1</c>) with deterministic bytes.
/// </summary>
public sealed class CanonicalSection
{
    public const string Schema = "mfc.canonical-section/1";
    public const string Version = "1";

    public CanonicalSection(
        CanonicalDomain domain,
        string sectionId,
        bool ordered,
        IReadOnlyList<CanonicalRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        ArgumentNullException.ThrowIfNull(records);
        Domain = domain;
        SectionId = sectionId;
        Ordered = ordered;
        Records = records;
        Utf8Bytes = WriteBytes(domain, sectionId, ordered, records);
    }

    public CanonicalDomain Domain { get; }

    public string SectionId { get; }

    public bool Ordered { get; }

    public IReadOnlyList<CanonicalRecord> Records { get; }

    public byte[] Utf8Bytes { get; }

    private static byte[] WriteBytes(
        CanonicalDomain domain,
        string sectionId,
        bool ordered,
        IReadOnlyList<CanonicalRecord> records)
    {
        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(Schema)),
            ("domain", w => w.WriteString(domain == CanonicalDomain.Configuration ? "configuration" : "observations")),
            ("section", w => w.WriteString(sectionId)),
            ("version", w => w.WriteString(Version)),
            ("ordered", w => w.WriteBoolean(ordered)),
            ("records", w =>
            {
                w.WriteArrayStart();
                for (int i = 0; i < records.Count; i++)
                {
                    if (i > 0)
                    {
                        w.WriteComma();
                    }

                    w.WriteSortedObject(records[i].Properties);
                }

                w.WriteArrayEnd();
            }),
        ]);
        return writer.ToUtf8Bytes();
    }
}
