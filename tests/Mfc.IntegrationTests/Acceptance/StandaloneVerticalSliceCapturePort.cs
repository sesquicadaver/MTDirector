using System.Text;
using System.Text.Json;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Snapshots;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.IntegrationTests.Acceptance;

/// <summary>
/// Lab-only capture port that emits deterministic standalone-topology sections.
/// Simulates CHR discovery outcomes without invoking the production write path (M1-30 AC#10/#11).
/// </summary>
public sealed class StandaloneVerticalSliceCapturePort : ISnapshotCapturePort
{
    private int _captureCount;

    public int CaptureCount => Volatile.Read(ref _captureCount);

    /// <summary>Filter rule action for the managed fwc rule (configuration domain).</summary>
    public string FilterAction { get; set; } = "accept";

    /// <summary>ether1 running observation (observation domain).</summary>
    public string InterfaceRunning { get; set; } = "true";

    public RouterOsReadTarget? LastTarget { get; private set; }

    public Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _captureCount);
        LastTarget = target;

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
                        ["name"] = "chr-standalone",
                        ["note"] = "m1-30",
                    },
                },
            ],
        });

        CanonicalSection filter = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = CanonicalSectionIds.FirewallIpv4Filter,
            Ordered = true,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["comment"] = "fwc:rule:aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee:v1",
                        ["chain"] = "forward",
                        ["action"] = FilterAction,
                        ["src-address"] = "203.0.113.10",
                    },
                },
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["comment"] = "fwc:rule:11111111-2222-4333-8444-555555555555:v1",
                        ["chain"] = "forward",
                        ["action"] = "accept",
                        ["dst-address"] = "198.51.100.1",
                    },
                },
            ],
        });

        CanonicalSection interfacesObs = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Observations,
            SectionId = CanonicalSectionIds.NetworkInterfaces,
            Ordered = false,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "ether1",
                        ["running"] = InterfaceRunning,
                        ["type"] = "ether",
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
                        ["api-ssl"] = "true",
                    },
                },
            ],
        });

        SnapshotHashBundle hashes = CanonicalizationService.HashSnapshotBundle(
            schemaVersion: 1,
            configurationSections: [identity, filter],
            observationSections: [interfacesObs]);

        CapabilityHash capabilityHash = CapabilityHash.FromDigest(CanonicalHashContract.HashSection(capability));

        byte[] raw = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                topology = "standalone",
                sanitized = true,
                apiSslCertificateVerified = true,
                filterAction = FilterAction,
                interfaceRunning = InterfaceRunning,
            }));

        return Task.FromResult(new SnapshotCaptureResult
        {
            ConfigurationHash = hashes.ConfigurationHash,
            ObservationHash = hashes.ObservationHash,
            CapabilityHash = capabilityHash,
            SnapshotHash = hashes.SnapshotHash,
            SchemaVersion = 1,
            RawPayload = raw,
            ConfigurationPayload = ConcatDocuments(identity, filter),
            ObservationPayload = interfacesObs.Utf8Bytes.ToArray(),
            CapabilityPayload = capability.Utf8Bytes.ToArray(),
            Sections =
            [
                new CapturedSectionDescriptor
                {
                    SectionId = identity.SectionId,
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = identity.Ordered,
                    ConfigurationRecordCount = identity.Records.Count,
                    ConfigurationPayload = identity.Utf8Bytes.ToArray(),
                },
                new CapturedSectionDescriptor
                {
                    SectionId = filter.SectionId,
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = filter.Ordered,
                    ConfigurationRecordCount = filter.Records.Count,
                    ConfigurationPayload = filter.Utf8Bytes.ToArray(),
                },
                new CapturedSectionDescriptor
                {
                    SectionId = interfacesObs.SectionId,
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = interfacesObs.Ordered,
                    ObservationRecordCount = interfacesObs.Records.Count,
                    ObservationPayload = interfacesObs.Utf8Bytes.ToArray(),
                },
                new CapturedSectionDescriptor
                {
                    SectionId = capability.SectionId,
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = capability.Ordered,
                    ConfigurationRecordCount = capability.Records.Count,
                    ConfigurationPayload = capability.Utf8Bytes.ToArray(),
                },
            ],
        });
    }

    private static byte[] ConcatDocuments(params CanonicalSection[] sections)
    {
        using MemoryStream stream = new();
        foreach (CanonicalSection section in sections)
        {
            stream.Write(section.Utf8Bytes);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }
}
