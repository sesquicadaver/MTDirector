namespace Mfc.Domain.Diff;

/// <summary>
/// Bounded ordered-sequence matching among still-unmatched records (Canonical Spec §33 MVP: unique LCS).
/// </summary>
internal static class OrderedDiff
{
    /// <summary>
    /// Matches unmatched records that share a fingerprint unique within the unmatched sets.
    /// Emits <see cref="MatchConfidence.ExactSequence"/> pairs; skips ambiguous duplicates (AC#13).
    /// </summary>
    public static IReadOnlyList<(DiffRecordView Base, DiffRecordView Target)> MatchUniqueFingerprintsInSequence(
        IReadOnlyList<DiffRecordView> unmatchedBase,
        IReadOnlyList<DiffRecordView> unmatchedTarget,
        DiffLimits limits,
        List<DiffWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(unmatchedBase);
        ArgumentNullException.ThrowIfNull(unmatchedTarget);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(warnings);

        int maxLen = Math.Max(unmatchedBase.Count, unmatchedTarget.Count);
        if (maxLen > limits.MaxOrderedRecords)
        {
            warnings.Add(new DiffWarning(
                "DIFF_COMPLEXITY_LIMIT",
                $"Ordered section unmatched size {maxLen} exceeds MaxOrderedRecords={limits.MaxOrderedRecords}."));
            return [];
        }

        long frontier = (long)unmatchedBase.Count * unmatchedTarget.Count;
        if (frontier > limits.MaxFrontierOps
            || Math.Abs(unmatchedBase.Count - unmatchedTarget.Count) > limits.MaxEditDistance)
        {
            warnings.Add(new DiffWarning(
                "DIFF_COMPLEXITY_LIMIT",
                "Ordered sequence matching exceeded edit-distance or frontier operation limits."));
            return [];
        }

        Dictionary<string, int> baseCounts = CountFingerprints(unmatchedBase);
        Dictionary<string, int> targetCounts = CountFingerprints(unmatchedTarget);

        // Only fingerprints unique on both unmatched sides — avoids false MOVED on duplicates.
        List<(DiffRecordView Base, DiffRecordView Target)> pairs = [];
        Dictionary<string, DiffRecordView> baseByFp = [];
        foreach (DiffRecordView record in unmatchedBase)
        {
            if (baseCounts[record.FingerprintHex] == 1
                && targetCounts.TryGetValue(record.FingerprintHex, out int tc)
                && tc == 1)
            {
                baseByFp[record.FingerprintHex] = record;
            }
        }

        HashSet<string> used = new(StringComparer.Ordinal);
        foreach (DiffRecordView target in unmatchedTarget)
        {
            if (!baseByFp.TryGetValue(target.FingerprintHex, out DiffRecordView? baseRecord))
            {
                continue;
            }

            if (!used.Add(target.FingerprintHex))
            {
                continue;
            }

            pairs.Add((baseRecord, target));
        }

        // Stable order by before ordinal then after ordinal.
        return pairs
            .OrderBy(static p => p.Base.Ordinal)
            .ThenBy(static p => p.Target.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, int> CountFingerprints(IReadOnlyList<DiffRecordView> records)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (DiffRecordView record in records)
        {
            counts[record.FingerprintHex] = counts.GetValueOrDefault(record.FingerprintHex) + 1;
        }

        return counts;
    }
}
