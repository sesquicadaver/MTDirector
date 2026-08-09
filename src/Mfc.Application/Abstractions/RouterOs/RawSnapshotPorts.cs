namespace Mfc.Application.Abstractions.RouterOs;

/// <summary>Thrown when a raw snapshot UTF-8 payload exceeds the configured maximum (M1-20).</summary>
public sealed class SnapshotPayloadTooLargeException : Exception
{
    public SnapshotPayloadTooLargeException(string message)
        : base(message)
    {
    }

    public SnapshotPayloadTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Application view of a redacted raw snapshot assembly (M1-20).</summary>
public sealed class RawSnapshotView
{
    public required int SchemaVersion { get; init; }

    public required int SectionCount { get; init; }

    public required int ByteLength { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Deterministic UTF-8 JSON payload (already redacted).</summary>
    public required byte[] Utf8Payload { get; init; }

    public required IReadOnlyList<RawSnapshotSectionView> Sections { get; init; }
}

public sealed class RawSnapshotSectionView
{
    public required string SourceMenu { get; init; }

    public required string CaptureStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required int RecordCount { get; init; }
}

/// <summary>Section input for application-level assembly (no RouterOS types).</summary>
public sealed class RawSnapshotSectionDraft
{
    public required string SourceMenu { get; init; }

    public required string CaptureStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required IReadOnlyList<RawSnapshotRecordDraft> Records { get; init; }
}

public sealed class RawSnapshotRecordDraft
{
    public required IReadOnlyDictionary<string, string> KnownProperties { get; init; }

    public required IReadOnlyDictionary<string, string> UnknownProperties { get; init; }
}

public sealed class AssembleRawSnapshotRequest
{
    public required IReadOnlyList<RawSnapshotSectionDraft> Sections { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Optional override for tests; production uses the compiled 256 MiB limit.</summary>
    public long? MaxSnapshotBytes { get; init; }
}

/// <summary>
/// Assembles and redacts a versioned raw snapshot.
/// Implementations live in Mfc.RouterOs and must strip secrets centrally.
/// </summary>
public interface IRawSnapshotAssemblerPort
{
    RawSnapshotView Assemble(AssembleRawSnapshotRequest request);
}
