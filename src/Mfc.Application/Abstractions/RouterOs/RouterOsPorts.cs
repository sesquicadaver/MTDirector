using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Abstractions.RouterOs;

/// <summary>
/// Application-facing RouterOS access. Implementations live in Mfc.RouterOs (M1-06+).
/// Credentials are referenced, never passed as plaintext passwords.
/// </summary>
public sealed class RouterOsReadTarget
{
    public required DeviceId DeviceId { get; init; }

    public required ManagementEndpoint Endpoint { get; init; }

    public required SecretReference SecretReference { get; init; }

    public required CertificateTrustMode TrustMode { get; init; }

    public string? CaProfileRef { get; init; }

    public Hash256? PinnedSpkiSha256 { get; init; }
}

public sealed class RouterOsProbeResult
{
    public required string Identity { get; init; }

    public required SupportState SupportState { get; init; }
}

/// <summary>Read-only RouterOS operations. Must never mutate device configuration.</summary>
public interface IRouterOsReadPort
{
    /// <summary>Lightweight identity/capability probe. Read-only; no RouterOS writes.</summary>
    Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Captures a device snapshot via the allowlisted read path.
/// Idempotent at the application boundary when the resulting snapshot hash already exists.
/// </summary>
public interface ISnapshotCapturePort
{
    Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default);
}

public sealed class SnapshotCaptureResult
{
    public required ConfigurationHash ConfigurationHash { get; init; }

    public required ObservationHash ObservationHash { get; init; }

    public required CapabilityHash CapabilityHash { get; init; }

    public required SnapshotHash SnapshotHash { get; init; }

    public required int SchemaVersion { get; init; }

    /// <summary>Uncompressed sanitized raw snapshot bytes (hashed before compression).</summary>
    public required ReadOnlyMemory<byte> RawPayload { get; init; }

    /// <summary>Uncompressed canonical configuration document bytes.</summary>
    public required ReadOnlyMemory<byte> ConfigurationPayload { get; init; }

    /// <summary>Uncompressed canonical observation document bytes.</summary>
    public required ReadOnlyMemory<byte> ObservationPayload { get; init; }

    /// <summary>Uncompressed canonical capability document bytes.</summary>
    public required ReadOnlyMemory<byte> CapabilityPayload { get; init; }

    /// <summary>Optional per-section mappings for <c>snapshot_capture_sections</c>.</summary>
    public IReadOnlyList<CapturedSectionDescriptor> Sections { get; init; } = [];
}

/// <summary>One menu section mapping persisted with a capture (Canonical Spec §28.2).</summary>
public sealed class CapturedSectionDescriptor
{
    public required string SectionId { get; init; }

    public required int SectionVersion { get; init; }

    public required short Status { get; init; }

    public required bool Ordered { get; init; }

    public int ConfigurationRecordCount { get; init; }

    public int ObservationRecordCount { get; init; }

    public ReadOnlyMemory<byte>? RawPayload { get; init; }

    public ReadOnlyMemory<byte>? ConfigurationPayload { get; init; }

    public ReadOnlyMemory<byte>? ObservationPayload { get; init; }
}
