namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Per-section mapping for a snapshot capture (Canonical Spec §28.2).
/// Rows are append-only once written with a completed capture.
/// </summary>
public sealed class SnapshotCaptureSectionEntity
{
    public Guid CaptureId { get; set; }

    public required string SectionId { get; set; }

    public int SectionVersion { get; set; }

    public short Status { get; set; }

    public bool Ordered { get; set; }

    public int ConfigurationRecordCount { get; set; }

    public int ObservationRecordCount { get; set; }

    public int CapabilityRecordCount { get; set; }

    public int CompatibilityRecordCount { get; set; }

    public byte[]? RawHash { get; set; }

    public byte[]? ConfigurationHash { get; set; }

    public byte[]? ObservationHash { get; set; }

    public byte[]? CapabilityHash { get; set; }

    public byte[]? CompatibilityHash { get; set; }
}
