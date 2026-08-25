using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Snapshots;
using Mfc.RouterOs.Commands;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Builds persist-ready <see cref="SnapshotCaptureResult"/> from a stable discovery dataset (M1-22 + M1-20).</summary>
public static class SnapshotCaptureResultBuilder
{
    public static SnapshotCaptureResult Build(RouterOsDiscoveryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        CanonicalDeviceSnapshot canonical = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            SchemaVersion = 1,
            System = dataset.System,
            Interfaces = dataset.Interfaces,
            Firewall = dataset.Firewall,
            Routing = dataset.Routing,
            Vrrp = dataset.Vrrp,
            BridgeSwitch = dataset.BridgeSwitch,
            Capabilities = dataset.Capabilities.Profile,
            PacketPathTopology = dataset.PacketPathTopology,
        });

        List<RawSectionCaptureInput> rawSections = dataset.CommandResults.Values
            .OrderBy(static r => (int)r.CommandId)
            .Select(RosReadCommandResultRawMapper.ToRawSection)
            .ToList();

        RawSnapshotAssemblyResult raw = RawSnapshotAssembler.Assemble(
            rawSections,
            new RawSnapshotCaptureTimestamps
            {
                StartedAtUtc = dataset.StartedAtUtc,
                CompletedAtUtc = dataset.CompletedAtUtc,
            });

        CanonicalSection capabilitySection = canonical.ConfigurationSections
            .Single(s => string.Equals(s.SectionId, CanonicalSectionIds.CapabilitiesDevice, StringComparison.Ordinal));

        List<CapturedSectionDescriptor> sections = BuildSectionDescriptors(canonical);

        return new SnapshotCaptureResult
        {
            ConfigurationHash = canonical.ConfigurationHash,
            ObservationHash = canonical.ObservationHash,
            CapabilityHash = dataset.Capabilities.CapabilityHash,
            SnapshotHash = canonical.SnapshotHash,
            SchemaVersion = canonical.SchemaVersion,
            RawPayload = raw.Utf8Payload,
            ConfigurationPayload = ConcatSections(canonical.ConfigurationSections),
            ObservationPayload = ConcatSections(canonical.ObservationSections),
            CapabilityPayload = capabilitySection.Utf8Bytes.ToArray(),
            Sections = sections,
        };
    }

    private static List<CapturedSectionDescriptor> BuildSectionDescriptors(CanonicalDeviceSnapshot canonical)
    {
        Dictionary<string, CapturedSectionDescriptor> bySectionId = new(StringComparer.Ordinal);
        foreach (CanonicalSection section in canonical.ConfigurationSections)
        {
            bySectionId[section.SectionId] = ToDescriptor(section, configuration: true);
        }

        foreach (CanonicalSection section in canonical.ObservationSections)
        {
            if (bySectionId.TryGetValue(section.SectionId, out CapturedSectionDescriptor? existing))
            {
                bySectionId[section.SectionId] = new CapturedSectionDescriptor
                {
                    SectionId = existing.SectionId,
                    SectionVersion = existing.SectionVersion,
                    Status = existing.Status,
                    Ordered = existing.Ordered || section.Ordered,
                    ConfigurationRecordCount = existing.ConfigurationRecordCount,
                    ConfigurationPayload = existing.ConfigurationPayload,
                    ObservationRecordCount = section.Records.Count,
                    ObservationPayload = section.Utf8Bytes.ToArray(),
                };
            }
            else
            {
                bySectionId[section.SectionId] = ToDescriptor(section, configuration: false);
            }
        }

        return bySectionId.Values.OrderBy(static s => s.SectionId, StringComparer.Ordinal).ToList();
    }

    private static CapturedSectionDescriptor ToDescriptor(CanonicalSection section, bool configuration)
    {
        if (configuration)
        {
            return new CapturedSectionDescriptor
            {
                SectionId = section.SectionId,
                SectionVersion = 1,
                Status = 1,
                Ordered = section.Ordered,
                ConfigurationRecordCount = section.Records.Count,
                ConfigurationPayload = section.Utf8Bytes.ToArray(),
            };
        }

        return new CapturedSectionDescriptor
        {
            SectionId = section.SectionId,
            SectionVersion = 1,
            Status = 1,
            Ordered = section.Ordered,
            ObservationRecordCount = section.Records.Count,
            ObservationPayload = section.Utf8Bytes.ToArray(),
        };
    }

    private static byte[] ConcatSections(IReadOnlyList<CanonicalSection> sections)
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
