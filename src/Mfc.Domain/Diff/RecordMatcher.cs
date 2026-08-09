using Mfc.Domain.Canonicalization;

namespace Mfc.Domain.Diff;

/// <summary>Phased conservative record matching (Canonical Spec §32).</summary>
internal static class RecordMatcher
{
    /// <summary>Matches records of one section pair and emits diff entries.</summary>
    public static IReadOnlyList<DiffEntry> MatchSection(
        CanonicalSection baseSection,
        CanonicalSection targetSection,
        DiffDomain domain,
        DiffLimits limits,
        List<DiffWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(baseSection);
        ArgumentNullException.ThrowIfNull(targetSection);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(warnings);

        List<DiffRecordView> baseRecords = ToViews(baseSection, domain);
        List<DiffRecordView> targetRecords = ToViews(targetSection, domain);
        bool[] baseMatched = new bool[baseRecords.Count];
        bool[] targetMatched = new bool[targetRecords.Count];
        List<DiffEntry> entries = [];

        bool ordered = baseSection.Ordered || targetSection.Ordered;
        MatchUniqueControllerIds(baseRecords, targetRecords, baseMatched, targetMatched, entries, ordered);
        MatchUniqueNaturalKeys(baseRecords, targetRecords, baseMatched, targetMatched, entries, ordered);
        MatchUniqueFingerprints(baseRecords, targetRecords, baseMatched, targetMatched, entries, ordered);

        if (baseSection.Ordered)
        {
            List<DiffRecordView> unmatchedBase = CollectUnmatched(baseRecords, baseMatched);
            List<DiffRecordView> unmatchedTarget = CollectUnmatched(targetRecords, targetMatched);

            if (Math.Max(baseRecords.Count, targetRecords.Count) > limits.MaxOrderedRecords)
            {
                warnings.Add(new DiffWarning(
                    "DIFF_COMPLEXITY_LIMIT",
                    $"Ordered section '{baseSection.SectionId}' has {Math.Max(baseRecords.Count, targetRecords.Count)} records; "
                    + $"limit is {limits.MaxOrderedRecords}. Falling back to conservative matching."));
            }
            else
            {
                IReadOnlyList<(DiffRecordView Base, DiffRecordView Target)> sequencePairs =
                    OrderedDiff.MatchUniqueFingerprintsInSequence(
                        unmatchedBase,
                        unmatchedTarget,
                        limits,
                        warnings);

                foreach ((DiffRecordView left, DiffRecordView right) in sequencePairs)
                {
                    MarkMatched(baseRecords, targetRecords, baseMatched, targetMatched, left, right);
                    DiffEntry? entry = ClassifyMatchedPair(
                        left,
                        right,
                        MatchConfidence.ExactSequence,
                        allowModified: false,
                        orderedSection: true);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
            }
        }

        // Phase 5 — conservative REMOVED / ADDED.
        for (int i = 0; i < baseRecords.Count; i++)
        {
            if (baseMatched[i])
            {
                continue;
            }

            DiffRecordView record = baseRecords[i];
            entries.Add(new DiffEntry(
                record.SectionId,
                record.Domain,
                [DiffChange.Removed],
                MatchConfidence.Conservative,
                record.RecordKey,
                beforeOrdinal: record.Ordinal,
                beforeProps: record.Properties));
        }

        for (int i = 0; i < targetRecords.Count; i++)
        {
            if (targetMatched[i])
            {
                continue;
            }

            DiffRecordView record = targetRecords[i];
            entries.Add(new DiffEntry(
                record.SectionId,
                record.Domain,
                [DiffChange.Added],
                MatchConfidence.Conservative,
                record.RecordKey,
                afterOrdinal: record.Ordinal,
                afterProps: record.Properties));
        }

        return entries;
    }

    public static IReadOnlyList<DiffEntry> AllAdded(CanonicalSection section, DiffDomain domain)
    {
        List<DiffEntry> entries = [];
        foreach (DiffRecordView record in ToViews(section, domain))
        {
            entries.Add(new DiffEntry(
                record.SectionId,
                domain,
                [DiffChange.Added],
                MatchConfidence.Conservative,
                record.RecordKey,
                afterOrdinal: record.Ordinal,
                afterProps: record.Properties));
        }

        return entries;
    }

    public static IReadOnlyList<DiffEntry> AllRemoved(CanonicalSection section, DiffDomain domain)
    {
        List<DiffEntry> entries = [];
        foreach (DiffRecordView record in ToViews(section, domain))
        {
            entries.Add(new DiffEntry(
                record.SectionId,
                domain,
                [DiffChange.Removed],
                MatchConfidence.Conservative,
                record.RecordKey,
                beforeOrdinal: record.Ordinal,
                beforeProps: record.Properties));
        }

        return entries;
    }

    private static List<DiffRecordView> ToViews(CanonicalSection section, DiffDomain domain)
    {
        List<DiffRecordView> views = new(section.Records.Count);
        for (int i = 0; i < section.Records.Count; i++)
        {
            views.Add(new DiffRecordView(section.Records[i], i, section.SectionId, domain));
        }

        return views;
    }

    private static void MatchUniqueControllerIds(
        List<DiffRecordView> baseRecords,
        List<DiffRecordView> targetRecords,
        bool[] baseMatched,
        bool[] targetMatched,
        List<DiffEntry> entries,
        bool orderedSection)
    {
        Dictionary<Guid, int> baseCounts = [];
        Dictionary<Guid, int> targetCounts = [];
        foreach (DiffRecordView record in baseRecords)
        {
            if (record.ControllerUuid is { } id)
            {
                baseCounts[id] = baseCounts.GetValueOrDefault(id) + 1;
            }
        }

        foreach (DiffRecordView record in targetRecords)
        {
            if (record.ControllerUuid is { } id)
            {
                targetCounts[id] = targetCounts.GetValueOrDefault(id) + 1;
            }
        }

        Dictionary<Guid, DiffRecordView> uniqueBase = [];
        for (int i = 0; i < baseRecords.Count; i++)
        {
            DiffRecordView record = baseRecords[i];
            if (record.ControllerUuid is not { } id || baseCounts[id] != 1 || targetCounts.GetValueOrDefault(id) != 1)
            {
                continue;
            }

            uniqueBase[id] = record;
        }

        for (int j = 0; j < targetRecords.Count; j++)
        {
            if (targetMatched[j])
            {
                continue;
            }

            DiffRecordView right = targetRecords[j];
            if (right.ControllerUuid is not { } id || !uniqueBase.TryGetValue(id, out DiffRecordView? left))
            {
                continue;
            }

            int bi = IndexOfUnmatched(baseRecords, baseMatched, left);
            if (bi < 0)
            {
                continue;
            }

            baseMatched[bi] = true;
            targetMatched[j] = true;
            DiffEntry? entry = ClassifyMatchedPair(
                left,
                right,
                MatchConfidence.ControllerId,
                allowModified: true,
                orderedSection: orderedSection);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }
    }

    private static void MatchUniqueNaturalKeys(
        List<DiffRecordView> baseRecords,
        List<DiffRecordView> targetRecords,
        bool[] baseMatched,
        bool[] targetMatched,
        List<DiffEntry> entries,
        bool orderedSection)
    {
        Dictionary<string, int> baseCounts = new(StringComparer.Ordinal);
        Dictionary<string, int> targetCounts = new(StringComparer.Ordinal);
        foreach (DiffRecordView record in baseRecords)
        {
            if (record.NaturalKey is { } key)
            {
                baseCounts[key] = baseCounts.GetValueOrDefault(key) + 1;
            }
        }

        foreach (DiffRecordView record in targetRecords)
        {
            if (record.NaturalKey is { } key)
            {
                targetCounts[key] = targetCounts.GetValueOrDefault(key) + 1;
            }
        }

        Dictionary<string, DiffRecordView> uniqueBase = new(StringComparer.Ordinal);
        for (int i = 0; i < baseRecords.Count; i++)
        {
            if (baseMatched[i])
            {
                continue;
            }

            DiffRecordView record = baseRecords[i];
            if (record.NaturalKey is not { } key
                || baseCounts[key] != 1
                || targetCounts.GetValueOrDefault(key) != 1)
            {
                continue;
            }

            uniqueBase[key] = record;
        }

        for (int j = 0; j < targetRecords.Count; j++)
        {
            if (targetMatched[j])
            {
                continue;
            }

            DiffRecordView right = targetRecords[j];
            if (right.NaturalKey is not { } key || !uniqueBase.TryGetValue(key, out DiffRecordView? left))
            {
                continue;
            }

            int bi = IndexOfUnmatched(baseRecords, baseMatched, left);
            if (bi < 0)
            {
                continue;
            }

            baseMatched[bi] = true;
            targetMatched[j] = true;
            DiffEntry? entry = ClassifyMatchedPair(
                left,
                right,
                MatchConfidence.NaturalKey,
                allowModified: true,
                orderedSection: orderedSection);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }
    }

    private static void MatchUniqueFingerprints(
        List<DiffRecordView> baseRecords,
        List<DiffRecordView> targetRecords,
        bool[] baseMatched,
        bool[] targetMatched,
        List<DiffEntry> entries,
        bool orderedSection)
    {
        Dictionary<string, int> baseCounts = new(StringComparer.Ordinal);
        Dictionary<string, int> targetCounts = new(StringComparer.Ordinal);
        for (int i = 0; i < baseRecords.Count; i++)
        {
            if (baseMatched[i])
            {
                continue;
            }

            string fp = baseRecords[i].FingerprintHex;
            baseCounts[fp] = baseCounts.GetValueOrDefault(fp) + 1;
        }

        for (int j = 0; j < targetRecords.Count; j++)
        {
            if (targetMatched[j])
            {
                continue;
            }

            string fp = targetRecords[j].FingerprintHex;
            targetCounts[fp] = targetCounts.GetValueOrDefault(fp) + 1;
        }

        Dictionary<string, DiffRecordView> uniqueBase = new(StringComparer.Ordinal);
        for (int i = 0; i < baseRecords.Count; i++)
        {
            if (baseMatched[i])
            {
                continue;
            }

            DiffRecordView record = baseRecords[i];
            if (baseCounts[record.FingerprintHex] != 1
                || targetCounts.GetValueOrDefault(record.FingerprintHex) != 1)
            {
                continue;
            }

            uniqueBase[record.FingerprintHex] = record;
        }

        for (int j = 0; j < targetRecords.Count; j++)
        {
            if (targetMatched[j])
            {
                continue;
            }

            DiffRecordView right = targetRecords[j];
            if (!uniqueBase.TryGetValue(right.FingerprintHex, out DiffRecordView? left))
            {
                continue;
            }

            int bi = IndexOfUnmatched(baseRecords, baseMatched, left);
            if (bi < 0)
            {
                continue;
            }

            baseMatched[bi] = true;
            targetMatched[j] = true;
            DiffEntry? entry = ClassifyMatchedPair(
                left,
                right,
                MatchConfidence.ExactFingerprint,
                allowModified: false,
                orderedSection: orderedSection);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }
    }

    private static DiffEntry? ClassifyMatchedPair(
        DiffRecordView left,
        DiffRecordView right,
        MatchConfidence confidence,
        bool allowModified,
        bool orderedSection)
    {
        IReadOnlyList<DiffFieldChange> fieldChanges = FieldDiffComparer.Compare(left.Properties, right.Properties);
        bool ordinalChanged = left.Ordinal != right.Ordinal;
        List<DiffChange> changes = [];

        if (left.Domain == DiffDomain.Observation || right.Domain == DiffDomain.Observation)
        {
            if (fieldChanges.Count > 0)
            {
                changes.Add(DiffChange.StateChanged);
            }

            if (ordinalChanged && orderedSection)
            {
                changes.Add(DiffChange.Moved);
            }
        }
        else
        {
            if (fieldChanges.Count > 0)
            {
                if (allowModified
                    && confidence is MatchConfidence.ControllerId or MatchConfidence.NaturalKey)
                {
                    changes.Add(DiffChange.Modified);
                }
                else
                {
                    // ExactFingerprint/ExactSequence/Conservative must never emit MODIFIED.
                    // Fingerprint match implies content-equal; defensive path returns null pair handling upstream.
                    return null;
                }
            }

            if (ordinalChanged && orderedSection)
            {
                changes.Add(DiffChange.Moved);
            }
        }

        if (changes.Count == 0)
        {
            return null;
        }

        return new DiffEntry(
            left.SectionId,
            left.Domain,
            changes,
            confidence,
            left.RecordKey,
            beforeOrdinal: left.Ordinal,
            afterOrdinal: right.Ordinal,
            beforeProps: left.Properties,
            afterProps: right.Properties,
            fieldChanges: fieldChanges);
    }

    private static List<DiffRecordView> CollectUnmatched(List<DiffRecordView> records, bool[] matched)
    {
        List<DiffRecordView> list = [];
        for (int i = 0; i < records.Count; i++)
        {
            if (!matched[i])
            {
                list.Add(records[i]);
            }
        }

        return list;
    }

    private static void MarkMatched(
        List<DiffRecordView> baseRecords,
        List<DiffRecordView> targetRecords,
        bool[] baseMatched,
        bool[] targetMatched,
        DiffRecordView left,
        DiffRecordView right)
    {
        int bi = IndexOfUnmatched(baseRecords, baseMatched, left);
        int ti = IndexOfUnmatched(targetRecords, targetMatched, right);
        if (bi >= 0)
        {
            baseMatched[bi] = true;
        }

        if (ti >= 0)
        {
            targetMatched[ti] = true;
        }
    }

    private static int IndexOfUnmatched(List<DiffRecordView> records, bool[] matched, DiffRecordView target)
    {
        for (int i = 0; i < records.Count; i++)
        {
            if (!matched[i] && ReferenceEquals(records[i], target))
            {
                return i;
            }
        }

        return -1;
    }
}
