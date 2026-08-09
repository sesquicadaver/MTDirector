using System.Text.Json;
using System.Text.Json.Serialization;
using Mfc.RouterOs.Redaction;

namespace Mfc.RouterOs.Snapshot;

/// <summary>
/// Assembles discovery section captures into a versioned, redacted raw snapshot (M1-20).
/// Centralized redaction via <see cref="SensitiveFieldRegistry"/>; deterministic UTF-8 JSON.
/// </summary>
public static class RawSnapshotAssembler
{
    public const string LoginSentenceMarker = "/login";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Builds a redacted raw snapshot envelope. Throws <see cref="RawSnapshotTooLargeException"/> when oversized.
    /// </summary>
    public static RawSnapshotAssemblyResult Assemble(
        IReadOnlyList<RawSectionCaptureInput> sections,
        RawSnapshotCaptureTimestamps timestamps,
        long maxSnapshotBytes = RawSnapshotLimits.MaxSnapshotBytes)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(timestamps);
        ValidateTimestamps(timestamps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSnapshotBytes);

        List<RawSnapshotSection> assembled = new(sections.Count);
        foreach (RawSectionCaptureInput section in sections.OrderBy(static s => s.SourceMenu, StringComparer.Ordinal))
        {
            ArgumentNullException.ThrowIfNull(section);
            ValidateSourceMenu(section.SourceMenu);
            assembled.Add(MapSection(section));
        }

        RawSnapshotDocument document = new()
        {
            SchemaVersion = RawSnapshotLimits.SchemaVersion,
            Sections = assembled,
        };

        RawSnapshotWireEnvelope wire = new()
        {
            SchemaVersion = document.SchemaVersion,
            Capture = new RawSnapshotWireCapture
            {
                StartedAtUtc = timestamps.StartedAtUtc.ToUniversalTime().ToString("O"),
                CompletedAtUtc = timestamps.CompletedAtUtc.ToUniversalTime().ToString("O"),
            },
            Sections = document.Sections.Select(ToWireSection).ToArray(),
        };

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(wire, SerializerOptions);
        if (utf8.LongLength > maxSnapshotBytes)
        {
            throw new RawSnapshotTooLargeException(utf8.LongLength, maxSnapshotBytes);
        }

        return new RawSnapshotAssemblyResult
        {
            Document = document,
            Timestamps = timestamps,
            Utf8Payload = utf8,
        };
    }

    /// <summary>Deserializes a previously assembled payload (tests / persistence round-trip).</summary>
    public static RawSnapshotWireEnvelope Deserialize(ReadOnlySpan<byte> utf8Payload)
        => JsonSerializer.Deserialize<RawSnapshotWireEnvelope>(utf8Payload, SerializerOptions)
           ?? throw new InvalidOperationException("Raw snapshot payload deserialized to null.");

    private static void ValidateTimestamps(RawSnapshotCaptureTimestamps timestamps)
    {
        if (timestamps.StartedAtUtc.Offset != TimeSpan.Zero
            || timestamps.CompletedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture timestamps must be UTC.", nameof(timestamps));
        }

        if (timestamps.CompletedAtUtc < timestamps.StartedAtUtc)
        {
            throw new ArgumentException("CompletedAtUtc must be >= StartedAtUtc.", nameof(timestamps));
        }
    }

    private static void ValidateSourceMenu(string sourceMenu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceMenu);
        if (sourceMenu.Contains(LoginSentenceMarker, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Raw snapshot sections must not include API login sentences.",
                nameof(sourceMenu));
        }
    }

    private static RawSnapshotSection MapSection(RawSectionCaptureInput section)
    {
        // Partial/failed errors are preserved — never masked as Completed.
        string status = section.CaptureStatus switch
        {
            RawSectionCaptureStatus.Completed => "completed",
            RawSectionCaptureStatus.PartialError => "partial_error",
            RawSectionCaptureStatus.Failed => "failed",
            RawSectionCaptureStatus.Unsupported => "unsupported",
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };

        if (section.CaptureStatus is RawSectionCaptureStatus.PartialError or RawSectionCaptureStatus.Failed
            && string.IsNullOrWhiteSpace(section.ErrorCode))
        {
            throw new ArgumentException(
                $"Section '{section.SourceMenu}' with status {section.CaptureStatus} must include ErrorCode.",
                nameof(section));
        }

        List<RawSnapshotRecord> records = new(section.Records.Count);
        foreach (RawRecordInput record in section.Records)
        {
            ArgumentNullException.ThrowIfNull(record);
            records.Add(new RawSnapshotRecord
            {
                Properties = SensitiveFieldRegistry.RedactForStorage(record.KnownProperties),
                UnknownProperties = SensitiveFieldRegistry.RedactForStorage(record.UnknownProperties),
            });
        }

        return new RawSnapshotSection
        {
            SourceMenu = section.SourceMenu.Trim(),
            CommandId = section.CommandId?.ToString(),
            CaptureStatus = status,
            ErrorCode = section.ErrorCode,
            ErrorMessage = section.ErrorMessage,
            Records = records,
        };
    }

    private static RawSnapshotWireSection ToWireSection(RawSnapshotSection section)
        => new()
        {
            SourceMenu = section.SourceMenu,
            CommandId = section.CommandId,
            CaptureStatus = section.CaptureStatus,
            ErrorCode = section.ErrorCode,
            ErrorMessage = section.ErrorMessage,
            Records = section.Records.Select(static r => new RawSnapshotWireRecord
            {
                Properties = r.Properties,
                UnknownProperties = r.UnknownProperties,
            }).ToArray(),
        };
}

/// <summary>Wire DTO for deterministic JSON (public for test deserialization).</summary>
public sealed class RawSnapshotWireEnvelope
{
    public required int SchemaVersion { get; init; }

    public required RawSnapshotWireCapture Capture { get; init; }

    public required IReadOnlyList<RawSnapshotWireSection> Sections { get; init; }
}

public sealed class RawSnapshotWireCapture
{
    public required string StartedAtUtc { get; init; }

    public required string CompletedAtUtc { get; init; }
}

public sealed class RawSnapshotWireSection
{
    public required string SourceMenu { get; init; }

    public string? CommandId { get; init; }

    public required string CaptureStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required IReadOnlyList<RawSnapshotWireRecord> Records { get; init; }
}

public sealed class RawSnapshotWireRecord
{
    public required IReadOnlyDictionary<string, string> Properties { get; init; }

    public required IReadOnlyDictionary<string, string> UnknownProperties { get; init; }
}
