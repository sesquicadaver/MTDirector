using Mfc.RouterOs.Commands;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Capture status for one raw snapshot section (M1-20).</summary>
public enum RawSectionCaptureStatus : byte
{
    Completed = 0,
    PartialError = 1,
    Failed = 2,
    Unsupported = 3,
}

/// <summary>Limits for raw snapshot assembly (Vertical Slice §10.5).</summary>
public static class RawSnapshotLimits
{
    /// <summary>Raw schema version produced by this assembler.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Maximum serialized UTF-8 payload size (256 MiB).</summary>
    public const long MaxSnapshotBytes = 256L * 1024L * 1024L;
}

/// <summary>Typed failure when assembled raw snapshot exceeds size limit (M1-20 AC#11).</summary>
public sealed class RawSnapshotTooLargeException : Exception
{
    public const string ErrorCode = "SNAPSHOT_TOO_LARGE";

    public RawSnapshotTooLargeException(long actualBytes, long maxBytes)
        : base($"Raw snapshot size {actualBytes} exceeds maximum {maxBytes} bytes.")
    {
        ActualBytes = actualBytes;
        MaxBytes = maxBytes;
    }

    public long ActualBytes { get; }

    public long MaxBytes { get; }
}

/// <summary>One discovery record prior to redaction.</summary>
public sealed class RawRecordInput
{
    public required IReadOnlyDictionary<string, string> KnownProperties { get; init; }

    public required IReadOnlyDictionary<string, string> UnknownProperties { get; init; }
}

/// <summary>One section capture supplied by discovery / stable-read.</summary>
public sealed class RawSectionCaptureInput
{
    /// <summary>RouterOS source menu path, e.g. <c>/ip/firewall/filter/print</c>.</summary>
    public required string SourceMenu { get; init; }

    public RosReadCommandId? CommandId { get; init; }

    public required RawSectionCaptureStatus CaptureStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required IReadOnlyList<RawRecordInput> Records { get; init; }
}

/// <summary>Redacted record persisted in the raw snapshot.</summary>
public sealed class RawSnapshotRecord
{
    public required IReadOnlyDictionary<string, string> Properties { get; init; }

    public required IReadOnlyDictionary<string, string> UnknownProperties { get; init; }
}

/// <summary>One section in the versioned raw snapshot.</summary>
public sealed class RawSnapshotSection
{
    public required string SourceMenu { get; init; }

    public string? CommandId { get; init; }

    public required string CaptureStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required IReadOnlyList<RawSnapshotRecord> Records { get; init; }
}

/// <summary>
/// Capture timestamps stored separately from configuration section data (M1-20 AC#8).
/// </summary>
public sealed class RawSnapshotCaptureTimestamps
{
    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }
}

/// <summary>Versioned raw snapshot document (configuration sections only — no login sentences).</summary>
public sealed class RawSnapshotDocument
{
    public required int SchemaVersion { get; init; }

    public required IReadOnlyList<RawSnapshotSection> Sections { get; init; }
}

/// <summary>Assembled raw snapshot with separate timestamps and serialized payload.</summary>
public sealed class RawSnapshotAssemblyResult
{
    public required RawSnapshotDocument Document { get; init; }

    public required RawSnapshotCaptureTimestamps Timestamps { get; init; }

    /// <summary>Deterministic UTF-8 JSON payload for the document + capture envelope.</summary>
    public required byte[] Utf8Payload { get; init; }

    public int ByteLength => Utf8Payload.Length;
}
