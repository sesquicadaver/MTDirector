namespace Mfc.Desktop.Services;

/// <summary>Kinds of rows shown in the virtualized record list.</summary>
public enum SnapshotRecordDomainKind
{
    Configuration,
    Observation,
}

/// <summary>Presentation model for one capture listed for a device.</summary>
public sealed class SnapshotCaptureListItem
{
    public required Guid CaptureId { get; init; }

    public required string StatusText { get; init; }

    public required string CompletedAtText { get; init; }

    public required uint SchemaVersion { get; init; }
}

/// <summary>Presentation model for one section in the viewer sidebar.</summary>
public sealed class SnapshotSectionListItem
{
    public required string SectionId { get; init; }

    public required string StatusText { get; init; }

    public required bool Ordered { get; init; }

    public required int ConfigurationRecordCount { get; init; }

    public required int ObservationRecordCount { get; init; }

    public required bool IsTechnicalOnly { get; init; }
}

/// <summary>One field line inside a record (read-only, sanitized).</summary>
public sealed class SnapshotFieldLine
{
    public required string Name { get; init; }

    public required string Value { get; init; }

    public string DisplayLine => $"{Name}={Value}";
}

/// <summary>One canonical record row for virtualized ListBox binding.</summary>
public sealed class SnapshotRecordListItem
{
    public required string StableKey { get; init; }

    public required string OrdinalText { get; init; }

    public required SnapshotRecordDomainKind Domain { get; init; }

    public required IReadOnlyList<SnapshotFieldLine> Fields { get; init; }

    public string DomainLabel => Domain switch
    {
        SnapshotRecordDomainKind.Configuration => "Configuration",
        SnapshotRecordDomainKind.Observation => "Observation",
        _ => Domain.ToString(),
    };

    /// <summary>True when the compact list line omits one or more fields.</summary>
    public bool HasMoreFields => Fields.Count > 4;

    public string SummaryLine
        => SnapshotPresentationIdentity.FormatRecordSummary(StableKey, OrdinalText, Fields, HasMoreFields);
}

/// <summary>Loaded capture header + sections for the viewer.</summary>
public sealed class SnapshotViewerLoadResult
{
    public required bool Succeeded { get; init; }

    public string? Error { get; init; }

    public Guid? DeviceId { get; init; }

    public Guid? CaptureId { get; init; }

    public string StatusText { get; init; } = "—";

    public uint SchemaVersion { get; init; }

    public string ConfigurationHashHex { get; init; } = "—";

    public string ObservationHashHex { get; init; } = "—";

    public string CapabilityHashHex { get; init; } = "—";

    public string SnapshotHashHex { get; init; } = "—";

    public string CompletedAtText { get; init; } = "—";

    public IReadOnlyList<SnapshotCaptureListItem> Captures { get; init; } = [];

    public IReadOnlyList<SnapshotSectionListItem> Sections { get; init; } = [];

    public IReadOnlyList<SnapshotRecordListItem> ConfigurationRecords { get; init; } = [];

    public IReadOnlyList<SnapshotRecordListItem> ObservationRecords { get; init; } = [];

    public bool IsLoading { get; init; }
}
