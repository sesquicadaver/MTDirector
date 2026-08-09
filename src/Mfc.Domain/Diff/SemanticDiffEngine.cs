using System.Globalization;
using Mfc.Domain.Canonicalization;

namespace Mfc.Domain.Diff;

/// <summary>
/// Deterministic semantic snapshot diff (Canonical Spec §29–35, M1-24).
/// Pure domain algorithm — no I/O, EF, or Application references.
/// </summary>
public static class SemanticDiffEngine
{
    private const string ComplexityLimitCode = "DIFF_COMPLEXITY_LIMIT";

    /// <summary>
    /// Compares two sets of canonical sections and returns a sorted <see cref="DiffDocument"/>.
    /// </summary>
    public static DiffDocument Compare(
        IReadOnlyList<CanonicalSection> baseSections,
        IReadOnlyList<CanonicalSection> targetSections,
        DiffLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(baseSections);
        ArgumentNullException.ThrowIfNull(targetSections);
        DiffLimits effectiveLimits = limits ?? DiffLimits.M1Default;

        Dictionary<(DiffDomain Domain, string SectionId), CanonicalSection> baseMap = Partition(baseSections);
        Dictionary<(DiffDomain Domain, string SectionId), CanonicalSection> targetMap = Partition(targetSections);

        List<(DiffDomain Domain, string SectionId)> keys = UnionSectionKeys(baseMap.Keys, targetMap.Keys);
        List<DiffEntry> entries = [];
        List<DiffWarning> warnings = [];

        foreach ((DiffDomain domain, string sectionId) in keys)
        {
            (DiffDomain Domain, string SectionId) key = (domain, sectionId);
            baseMap.TryGetValue(key, out CanonicalSection? baseSection);
            targetMap.TryGetValue(key, out CanonicalSection? targetSection);

            if (baseSection is null && targetSection is null)
            {
                continue;
            }

            if (baseSection is null)
            {
                entries.AddRange(RecordMatcher.AllAdded(targetSection!, domain));
                continue;
            }

            if (targetSection is null)
            {
                entries.AddRange(RecordMatcher.AllRemoved(baseSection, domain));
                continue;
            }

            if (baseSection.Utf8Bytes.AsSpan().SequenceEqual(targetSection.Utf8Bytes))
            {
                continue;
            }

            entries.AddRange(
                RecordMatcher.MatchSection(baseSection, targetSection, domain, effectiveLimits, warnings));
        }

        DiffEntry[] sorted = SortEntries(entries);
        bool hasComplexityHardFail = warnings.Exists(static w =>
            string.Equals(w.Code, ComplexityLimitCode, StringComparison.Ordinal));
        bool identical = sorted.Length == 0 && !hasComplexityHardFail;
        return new DiffDocument(sorted, warnings, identical);
    }

    private static Dictionary<(DiffDomain Domain, string SectionId), CanonicalSection> Partition(
        IReadOnlyList<CanonicalSection> sections)
    {
        Dictionary<(DiffDomain Domain, string SectionId), CanonicalSection> map = [];
        foreach (CanonicalSection section in sections)
        {
            DiffDomain domain = MapDomain(section);
            map[(domain, section.SectionId)] = section;
        }

        return map;
    }

    private static DiffDomain MapDomain(CanonicalSection section)
    {
        if (section.SectionId.StartsWith("capabilities.", StringComparison.Ordinal))
        {
            return DiffDomain.Capability;
        }

        if (section.SectionId.StartsWith("compatibility.", StringComparison.Ordinal))
        {
            return DiffDomain.Compatibility;
        }

        return section.Domain == CanonicalDomain.Configuration
            ? DiffDomain.Configuration
            : DiffDomain.Observation;
    }

    private static List<(DiffDomain Domain, string SectionId)> UnionSectionKeys(
        IEnumerable<(DiffDomain Domain, string SectionId)> left,
        IEnumerable<(DiffDomain Domain, string SectionId)> right)
    {
        HashSet<(DiffDomain Domain, string SectionId)> set = [.. left, .. right];
        return set
            .OrderBy(k => CanonicalSectionIds.RegistryOrderIndex.GetValueOrDefault(k.SectionId, int.MaxValue))
            .ThenBy(static k => k.SectionId, StringComparer.Ordinal)
            .ThenBy(static k => (byte)k.Domain)
            .ToList();
    }

    private static DiffEntry[] SortEntries(List<DiffEntry> entries)
        => entries
            .OrderBy(e => CanonicalSectionIds.RegistryOrderIndex.GetValueOrDefault(e.SectionId, int.MaxValue))
            .ThenBy(e => e.SectionId, StringComparer.Ordinal)
            .ThenBy(e => (byte)e.Domain)
            .ThenBy(e => e.BeforeOrdinal ?? int.MaxValue)
            .ThenBy(e => e.AfterOrdinal ?? int.MaxValue)
            .ThenBy(e => e.RecordKey, StringComparer.Ordinal)
            .ThenBy(e => ChangeOrderKey(e.Changes))
            .ToArray();

    private static string ChangeOrderKey(IReadOnlyList<DiffChange> changes)
        => string.Join(
            ',',
            changes.Select(static c => ((byte)c).ToString(CultureInfo.InvariantCulture)));
}
