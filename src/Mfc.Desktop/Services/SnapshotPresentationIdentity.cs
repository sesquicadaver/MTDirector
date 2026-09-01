namespace Mfc.Desktop.Services;

/// <summary>
/// Operator-facing identity for snapshot/diff rows. Fingerprint hex stays on the wire
/// <c>StableKey</c>/<c>RecordKey</c> but must not lead the list line.
/// </summary>
public static class SnapshotPresentationIdentity
{
    public const int FingerprintHexLength = 64;

    /// <summary>True when <paramref name="key"/> is a 64-char SHA-256 hex fingerprint.</summary>
    public static bool IsFingerprintKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length != FingerprintHexLength)
        {
            return false;
        }

        foreach (char c in key)
        {
            bool hex = (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }

    public static string FormatRecordSummary(
        string stableKey,
        string ordinalText,
        IReadOnlyList<SnapshotFieldLine> fields,
        bool hasMoreFields)
    {
        ArgumentNullException.ThrowIfNull(stableKey);
        ArgumentNullException.ThrowIfNull(ordinalText);
        ArgumentNullException.ThrowIfNull(fields);
        string compact = string.Join(
            "; ",
            fields.Take(4).Select(static f => $"{f.Name}={f.Value}"));
        string suffix = hasMoreFields ? " …" : string.Empty;
        if (string.IsNullOrWhiteSpace(compact))
        {
            if (IsFingerprintKey(stableKey))
            {
                return HasUsefulOrdinal(ordinalText) ? "#" + ordinalText : "—";
            }

            return stableKey;
        }

        if (IsFingerprintKey(stableKey))
        {
            string prefix = HasUsefulOrdinal(ordinalText) ? "#" + ordinalText + " · " : string.Empty;
            return prefix + compact + suffix;
        }

        return $"{stableKey} · {compact}{suffix}";
    }

    public static string FormatDiffIdentity(
        string recordKey,
        string ordinalText,
        IReadOnlyList<SnapshotDiffFieldLine> fieldLines)
    {
        ArgumentNullException.ThrowIfNull(recordKey);
        ArgumentNullException.ThrowIfNull(ordinalText);
        ArgumentNullException.ThrowIfNull(fieldLines);
        if (!IsFingerprintKey(recordKey))
        {
            return string.IsNullOrWhiteSpace(recordKey) ? "—" : recordKey;
        }

        string fromFields = string.Join("; ", fieldLines.Take(4).Select(static f => f.Summary));
        if (!string.IsNullOrWhiteSpace(fromFields))
        {
            return fromFields;
        }

        return HasUsefulOrdinal(ordinalText) ? ordinalText : "unmanaged record";
    }

    public static SnapshotSectionListItem? PreferOperatorFacingSection(
        IReadOnlyList<SnapshotSectionListItem> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        foreach (string id in OperatorFacingSectionIds)
        {
            SnapshotSectionListItem? match = sections.FirstOrDefault(s =>
                string.Equals(s.SectionId, id, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return sections.Count == 0 ? null : sections[0];
    }

    public static IReadOnlyList<SnapshotSectionListItem> OrderOperatorFacing(
        IEnumerable<SnapshotSectionListItem> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return sections
            .OrderBy(static s => OperatorFacingRank(s.SectionId))
            .ThenBy(static s => s.SectionId, StringComparer.Ordinal)
            .ToArray();
    }

    public static readonly string[] OperatorFacingSectionIds =
    [
        "firewall.ipv4.filter",
        "firewall.ipv6.filter",
        "ha.vrrp",
        "system.identity",
        "system.resource",
    ];

    private static int OperatorFacingRank(string sectionId)
    {
        for (int i = 0; i < OperatorFacingSectionIds.Length; i++)
        {
            if (string.Equals(sectionId, OperatorFacingSectionIds[i], StringComparison.Ordinal))
            {
                return i;
            }
        }

        return OperatorFacingSectionIds.Length;
    }

    private static bool HasUsefulOrdinal(string ordinalText)
        => !string.IsNullOrWhiteSpace(ordinalText)
           && !string.Equals(ordinalText, "—", StringComparison.Ordinal)
           && !ordinalText.StartsWith("order: —", StringComparison.Ordinal);
}
