namespace Mfc.Desktop.Services;

/// <summary>One field-level change line mirrored from DiffEntry.FieldDiff (no reinterpretation).</summary>
public sealed class SnapshotDiffFieldLine
{
    public required string FieldName { get; init; }

    public required string Summary { get; init; }

    public string FriendlySummary => DesktopDisplayLabels.FormatDiffFieldSummary(FieldName, Summary);
}

/// <summary>One DiffEntry presentation row for virtualized ListBox binding.</summary>
public sealed class SnapshotDiffEntryItem
{
    public required string SectionId { get; init; }

    public required string DomainText { get; init; }

    public required string ChangesText { get; init; }

    public required string RecordKey { get; init; }

    public required string OrdinalText { get; init; }

    public required string ConfidenceText { get; init; }

    public required IReadOnlyList<SnapshotDiffFieldLine> FieldLines { get; init; }

    /// <summary>Sanitized fields from DiffEntry.Before SnapshotRecord (empty when wire omitted the record).</summary>
    public IReadOnlyList<SnapshotDiffFieldLine> BeforeRecordFields { get; init; } = [];

    /// <summary>Sanitized fields from DiffEntry.After SnapshotRecord (empty when wire omitted the record).</summary>
    public IReadOnlyList<SnapshotDiffFieldLine> AfterRecordFields { get; init; } = [];

    public bool HasBeforeRecord { get; init; }

    public bool HasAfterRecord { get; init; }

    public string BeforeStableKey { get; init; } = string.Empty;

    public string AfterStableKey { get; init; } = string.Empty;

    public bool HasRecordSides => HasBeforeRecord || HasAfterRecord;

    public string DisplayIdentity =>
        SnapshotPresentationIdentity.FormatDiffIdentity(RecordKey, OrdinalText, FieldLines);

    public string OperatorDisplayIdentity =>
        DesktopDisplayLabels.FormatDiffIdentityFriendly(RecordKey, OrdinalText, FieldLines);

    public string SectionTitle => DesktopDisplayLabels.FormatSectionTitle(SectionId);

    public string HeaderLine =>
        $"{SectionTitle} · {DomainText} · {ChangesText} · {OperatorDisplayIdentity} · {OrdinalText}";
}

/// <summary>Section group for sidebar navigation.</summary>
public sealed class SnapshotDiffSectionGroup
{
    public required string SectionId { get; init; }

    public required int EntryCount { get; init; }

    public required IReadOnlyList<SnapshotDiffEntryItem> Entries { get; init; }

    public string SectionTitle => DesktopDisplayLabels.FormatSectionTitle(SectionId);
}

/// <summary>Result of a server CompareSnapshots load for Desktop presentation.</summary>
public sealed class SnapshotDiffLoadResult
{
    public required bool Succeeded { get; init; }

    public string? Error { get; init; }

    public Guid? LeftCaptureId { get; init; }

    public Guid? RightCaptureId { get; init; }

    public bool Identical { get; init; }

    public bool IsNoDifferences { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<SnapshotCaptureListItem> Captures { get; init; } = [];

    public IReadOnlyList<SnapshotDiffSectionGroup> SectionGroups { get; init; } = [];

    public IReadOnlyList<SnapshotDiffEntryItem> AllEntries { get; init; } = [];

    public bool IsLoading { get; init; }
}
