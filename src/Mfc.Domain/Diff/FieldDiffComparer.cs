namespace Mfc.Domain.Diff;

/// <summary>Field-level comparison for matched records (Canonical Spec §34).</summary>
internal static class FieldDiffComparer
{
    private static readonly HashSet<string> SetFields = new(StringComparer.Ordinal)
    {
        "members",
        "include",
        "exclude",
        "addresses",
        "tagged",
        "untagged",
    };

    /// <summary>
    /// Compares property dictionaries, skipping ordinal keys. Set-like CSV fields yield added/removed values.
    /// </summary>
    public static IReadOnlyList<DiffFieldChange> Compare(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string key in before.Keys)
        {
            if (!RecordFingerprint.IsExcludedKey(key))
            {
                names.Add(key);
            }
        }

        foreach (string key in after.Keys)
        {
            if (!RecordFingerprint.IsExcludedKey(key))
            {
                names.Add(key);
            }
        }

        List<DiffFieldChange> changes = [];
        foreach (string name in names.OrderBy(static n => n, StringComparer.Ordinal))
        {
            before.TryGetValue(name, out string? left);
            after.TryGetValue(name, out string? right);
            left ??= string.Empty;
            right ??= string.Empty;
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                continue;
            }

            if (SetFields.Contains(name))
            {
                HashSet<string> leftSet = ParseCsvSet(left);
                HashSet<string> rightSet = ParseCsvSet(right);
                string[] added = rightSet.Except(leftSet, StringComparer.Ordinal)
                    .OrderBy(static s => s, StringComparer.Ordinal)
                    .ToArray();
                string[] removed = leftSet.Except(rightSet, StringComparer.Ordinal)
                    .OrderBy(static s => s, StringComparer.Ordinal)
                    .ToArray();
                if (added.Length == 0 && removed.Length == 0)
                {
                    continue;
                }

                changes.Add(new DiffFieldChange(
                    name,
                    before: left.Length == 0 ? null : left,
                    after: right.Length == 0 ? null : right,
                    addedValues: added,
                    removedValues: removed));
            }
            else
            {
                changes.Add(new DiffFieldChange(
                    name,
                    before: left.Length == 0 ? null : left,
                    after: right.Length == 0 ? null : right));
            }
        }

        return changes;
    }

    /// <summary>True when non-ordinal properties are equal (set fields compared as sets).</summary>
    public static bool ContentEqualExceptOrdinal(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after)
        => Compare(before, after).Count == 0;

    private static HashSet<string> ParseCsvSet(string csv)
    {
        HashSet<string> set = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(csv))
        {
            return set;
        }

        foreach (string part in csv.Split(',', StringSplitOptions.None))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                set.Add(trimmed);
            }
        }

        return set;
    }
}
