namespace Mfc.Domain.Diff;

/// <summary>Scalar or set-valued field difference (Canonical Spec §34).</summary>
public sealed class DiffFieldChange
{
    public DiffFieldChange(
        string fieldName,
        string? before = null,
        string? after = null,
        IReadOnlyList<string>? addedValues = null,
        IReadOnlyList<string>? removedValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        FieldName = fieldName;
        Before = before;
        After = after;
        AddedValues = addedValues ?? [];
        RemovedValues = removedValues ?? [];
    }

    public string FieldName { get; }

    public string? Before { get; }

    public string? After { get; }

    public IReadOnlyList<string> AddedValues { get; }

    public IReadOnlyList<string> RemovedValues { get; }
}

/// <summary>One semantic record-level difference entry (§30 DiffEntry).</summary>
public sealed class DiffEntry
{
    public DiffEntry(
        string sectionId,
        DiffDomain domain,
        IReadOnlyList<DiffChange> changes,
        MatchConfidence confidence,
        string recordKey,
        int? beforeOrdinal = null,
        int? afterOrdinal = null,
        IReadOnlyDictionary<string, string>? beforeProps = null,
        IReadOnlyDictionary<string, string>? afterProps = null,
        IReadOnlyList<DiffFieldChange>? fieldChanges = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordKey);
        SectionId = sectionId;
        Domain = domain;
        Changes = NormalizeChangeOrder(changes);
        Confidence = confidence;
        RecordKey = recordKey;
        BeforeOrdinal = beforeOrdinal;
        AfterOrdinal = afterOrdinal;
        BeforeProps = beforeProps;
        AfterProps = afterProps;
        FieldChanges = fieldChanges ?? [];
    }

    public string SectionId { get; }

    public DiffDomain Domain { get; }

    public IReadOnlyList<DiffChange> Changes { get; }

    public MatchConfidence Confidence { get; }

    public string RecordKey { get; }

    public int? BeforeOrdinal { get; }

    public int? AfterOrdinal { get; }

    public IReadOnlyDictionary<string, string>? BeforeProps { get; }

    public IReadOnlyDictionary<string, string>? AfterProps { get; }

    public IReadOnlyList<DiffFieldChange> FieldChanges { get; }

    private static DiffChange[] NormalizeChangeOrder(IReadOnlyList<DiffChange> changes)
        => changes.Distinct().OrderBy(static c => (byte)c).ToArray();
}

/// <summary>Non-fatal diff warning (complexity / degraded matching).</summary>
public sealed class DiffWarning
{
    public DiffWarning(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}

/// <summary>Complete deterministic semantic diff document.</summary>
public sealed class DiffDocument
{
    public DiffDocument(
        IReadOnlyList<DiffEntry> entries,
        IReadOnlyList<DiffWarning> warnings,
        bool identical)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(warnings);
        Entries = entries;
        Warnings = warnings;
        Identical = identical;
    }

    public IReadOnlyList<DiffEntry> Entries { get; }

    public IReadOnlyList<DiffWarning> Warnings { get; }

    public bool Identical { get; }
}

/// <summary>Bounded ordered-diff limits for M1 (Canonical Spec §33.2).</summary>
public sealed class DiffLimits
{
    public DiffLimits(int maxOrderedRecords, int maxEditDistance, long maxFrontierOps)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOrderedRecords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEditDistance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrontierOps);
        MaxOrderedRecords = maxOrderedRecords;
        MaxEditDistance = maxEditDistance;
        MaxFrontierOps = maxFrontierOps;
    }

    public int MaxOrderedRecords { get; }

    public int MaxEditDistance { get; }

    public long MaxFrontierOps { get; }

    /// <summary>Default M1 limits from Canonical Spec §33.2.</summary>
    public static DiffLimits M1Default { get; } = new(
        maxOrderedRecords: 20_000,
        maxEditDistance: 4096,
        maxFrontierOps: 8_000_000);
}
