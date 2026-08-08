namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Per-device snapshot capture metadata. Completed rows are immutable (Vertical Slice §8.8).
/// </summary>
public sealed class SnapshotCaptureEntity
{
    /// <summary>Completed status value aligned with <c>Mfc.Domain.Snapshots.SnapshotStatus.Completed</c>.</summary>
    public const short CompletedStatus = 8;

    public Guid Id { get; set; }

    public Guid OperationId { get; set; }

    public Guid DeviceId { get; set; }

    public short Status { get; set; }

    public short AttemptCount { get; set; }

    public DateTimeOffset CaptureStartedAtUtc { get; set; }

    public DateTimeOffset? Pass1CompletedAtUtc { get; set; }

    public DateTimeOffset? Pass2CompletedAtUtc { get; set; }

    public DateTimeOffset? CaptureCompletedAtUtc { get; set; }

    public byte[]? RawPayloadHash { get; set; }

    public byte[]? ConfigurationPayloadHash { get; set; }

    public byte[]? ObservationPayloadHash { get; set; }

    public byte[]? CapabilityPayloadHash { get; set; }

    public byte[]? CompatibilityPayloadHash { get; set; }

    public byte[]? ConfigurationHash { get; set; }

    public byte[]? ObservationHash { get; set; }

    public byte[]? CapabilityHash { get; set; }

    public byte[]? CompatibilityMaterialHash { get; set; }

    public byte[]? SnapshotHash { get; set; }

    public required string SectionResultsJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorDetailsJson { get; set; }
}
