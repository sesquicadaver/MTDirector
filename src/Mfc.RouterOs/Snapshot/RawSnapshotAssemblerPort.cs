using Mfc.Application.Abstractions.RouterOs;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Application port adapter for raw snapshot assembly (M1-20).</summary>
public sealed class RawSnapshotAssemblerPort : IRawSnapshotAssemblerPort
{
    public RawSnapshotView Assemble(AssembleRawSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Sections);

        List<RawSectionCaptureInput> sections = new(request.Sections.Count);
        foreach (RawSnapshotSectionDraft draft in request.Sections)
        {
            ArgumentNullException.ThrowIfNull(draft);
            sections.Add(new RawSectionCaptureInput
            {
                SourceMenu = draft.SourceMenu,
                CommandId = null,
                CaptureStatus = ParseStatus(draft.CaptureStatus),
                ErrorCode = draft.ErrorCode,
                ErrorMessage = draft.ErrorMessage,
                Records = draft.Records.Select(static r => new RawRecordInput
                {
                    KnownProperties = r.KnownProperties,
                    UnknownProperties = r.UnknownProperties,
                }).ToArray(),
            });
        }

        RawSnapshotCaptureTimestamps timestamps = new()
        {
            StartedAtUtc = request.StartedAtUtc,
            CompletedAtUtc = request.CompletedAtUtc,
        };

        long maxBytes = request.MaxSnapshotBytes ?? RawSnapshotLimits.MaxSnapshotBytes;
        try
        {
            RawSnapshotAssemblyResult result = RawSnapshotAssembler.Assemble(sections, timestamps, maxBytes);
            return new RawSnapshotView
            {
                SchemaVersion = result.Document.SchemaVersion,
                SectionCount = result.Document.Sections.Count,
                ByteLength = result.ByteLength,
                StartedAtUtc = result.Timestamps.StartedAtUtc,
                CompletedAtUtc = result.Timestamps.CompletedAtUtc,
                Utf8Payload = result.Utf8Payload,
                Sections = result.Document.Sections.Select(static s => new RawSnapshotSectionView
                {
                    SourceMenu = s.SourceMenu,
                    CaptureStatus = s.CaptureStatus,
                    ErrorCode = s.ErrorCode,
                    ErrorMessage = s.ErrorMessage,
                    RecordCount = s.Records.Count,
                }).ToArray(),
            };
        }
        catch (RawSnapshotTooLargeException ex)
        {
            throw new SnapshotPayloadTooLargeException(ex.Message, ex);
        }
    }

    private static RawSectionCaptureStatus ParseStatus(string status)
        => status.Trim().ToLowerInvariant() switch
        {
            "completed" => RawSectionCaptureStatus.Completed,
            "partial_error" => RawSectionCaptureStatus.PartialError,
            "failed" => RawSectionCaptureStatus.Failed,
            "unsupported" => RawSectionCaptureStatus.Unsupported,
            _ => throw new ArgumentException($"Unknown capture status '{status}'.", nameof(status)),
        };
}
