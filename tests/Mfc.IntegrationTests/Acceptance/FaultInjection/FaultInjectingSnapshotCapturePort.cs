using System.Text;
using System.Text.Json;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Snapshots;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.IntegrationTests.Acceptance.FaultInjection;

/// <summary>
/// Lab-only capture port for M1-33 snapshot fault injection (no product RouterOS writes).
/// </summary>
public sealed class FaultInjectingSnapshotCapturePort : ISnapshotCapturePort
{
    public enum CaptureMode
    {
        Succeed,
        Unstable,
        Oversized,
        DependencyFault,
        HangUntilCancelled,
    }

    private int _captureCount;
    private int _successCount;

    public int CaptureCount => Volatile.Read(ref _captureCount);

    public int SuccessCount => Volatile.Read(ref _successCount);

    public CaptureMode Mode { get; set; } = CaptureMode.Succeed;

    public string Note { get; set; } = "m1-33";

    public Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        Interlocked.Increment(ref _captureCount);
        cancellationToken.ThrowIfCancellationRequested();

        return Mode switch
        {
            CaptureMode.Unstable => Task.FromException<SnapshotCaptureResult>(
                new InvalidOperationException("SNAPSHOT_UNSTABLE: configuration changed during capture")),
            CaptureMode.Oversized => Task.FromException<SnapshotCaptureResult>(
                new InvalidOperationException("SNAPSHOT_TOO_LARGE: synthetic oversized payload")),
            CaptureMode.DependencyFault => Task.FromException<SnapshotCaptureResult>(
                new IOException("synthetic transport fault")),
            CaptureMode.HangUntilCancelled => HangAsync(cancellationToken),
            _ => Task.FromResult(BuildSuccess()),
        };
    }

    private static async Task<SnapshotCaptureResult> HangAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException("HangUntilCancelled resumed unexpectedly.");
    }

    private SnapshotCaptureResult BuildSuccess()
    {
        Interlocked.Increment(ref _successCount);
        CanonicalSection identity = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = CanonicalSectionIds.SystemIdentity,
            Ordered = false,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "chr-fault-injection",
                        ["note"] = Note,
                    },
                },
            ],
        });

        CanonicalSection capability = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = CanonicalSectionIds.CapabilitiesDevice,
            Ordered = false,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["profile"] = "chr-x86_64",
                        ["fault-suite"] = "m1-33",
                    },
                },
            ],
        });

        SnapshotHashBundle hashes = CanonicalizationService.HashSnapshotBundle(
            schemaVersion: 1,
            configurationSections: [identity],
            observationSections: []);
        CapabilityHash capabilityHash = CapabilityHash.FromDigest(CanonicalHashContract.HashSection(capability));
        byte[] raw = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { topology = "fault-injection", Note }));

        return new SnapshotCaptureResult
        {
            ConfigurationHash = hashes.ConfigurationHash,
            ObservationHash = hashes.ObservationHash,
            CapabilityHash = capabilityHash,
            SnapshotHash = hashes.SnapshotHash,
            SchemaVersion = 1,
            RawPayload = raw,
            ConfigurationPayload = identity.Utf8Bytes.ToArray(),
            ObservationPayload = "[]"u8.ToArray(),
            CapabilityPayload = capability.Utf8Bytes.ToArray(),
            Sections =
            [
                new CapturedSectionDescriptor
                {
                    SectionId = identity.SectionId,
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = false,
                    ConfigurationRecordCount = 1,
                    ConfigurationPayload = identity.Utf8Bytes.ToArray(),
                },
                new CapturedSectionDescriptor
                {
                    SectionId = capability.SectionId,
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = false,
                    ConfigurationRecordCount = 1,
                    ConfigurationPayload = capability.Utf8Bytes.ToArray(),
                },
            ],
        };
    }
}
