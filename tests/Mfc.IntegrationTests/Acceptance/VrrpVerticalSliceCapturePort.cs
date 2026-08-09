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
/// Lab-only capture port for VRRP active/passive and split-master topologies (M1-32).
/// Emits ha.vrrp configuration/observation sections without product write commands.
/// </summary>
public sealed class VrrpVerticalSliceCapturePort : ISnapshotCapturePort
{
    public enum TopologyMode
    {
        ActivePassive,
        SplitMaster,
    }

    private int _captureCount;

    public int CaptureCount => Volatile.Read(ref _captureCount);

    public TopologyMode Mode { get; set; } = TopologyMode.ActivePassive;

    /// <summary>Observed role for primary member (.10).</summary>
    public string PrimaryRole { get; set; } = "Master";

    /// <summary>Observed role for secondary member (.11).</summary>
    public string SecondaryRole { get; set; } = "Backup";

    public string PrimaryHost { get; set; } = "10.255.40.10";

    public string SecondaryHost { get; set; } = "10.255.40.11";

    public RouterOsReadTarget? LastTarget { get; private set; }

    public Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _captureCount);
        LastTarget = target;

        string host = target.Endpoint.Host.Value;
        bool isPrimary = string.Equals(host, PrimaryHost, StringComparison.Ordinal);
        bool isSecondary = string.Equals(host, SecondaryHost, StringComparison.Ordinal);
        if (!isPrimary && !isSecondary)
        {
            throw new InvalidOperationException($"Unexpected VRRP member host '{host}'.");
        }

        string memberRole = isPrimary ? PrimaryRole : SecondaryRole;
        string topologyId = Mode == TopologyMode.ActivePassive ? "vrrp-active-passive" : "vrrp-split-master";
        string vip = Mode == TopologyMode.ActivePassive ? "10.255.40.20/24" : "10.255.50.20/24";
        string memberLabel = isPrimary ? "primary" : "secondary";
        // Priority stays configuration-stable across role switches (AC#6/#7).
        int priority = isPrimary ? 200 : 100;

        // AC#3: role vector is per VRID (two groups), not a single global device role.
        string[] vrids = Mode == TopologyMode.ActivePassive ? ["10", "20"] : ["10"];
        List<Dictionary<string, string>> configRecords = [];
        List<Dictionary<string, string>> obsRecords = [];
        foreach (string vrid in vrids)
        {
            string group = $"Ipv4/vrid={vrid}/if=ether1";
            string name = $"vrrp-{vrid}";
            configRecords.Add(Props(
                ("group", group),
                ("name", name),
                ("priority", priority.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("version", "3"),
                ("interval", "1s"),
                ("preemption-mode", "yes"),
                ("disabled", "false"),
                ("addresses", vip)));
            obsRecords.Add(Props(
                ("group", group),
                ("role", memberRole),
                ("running", "true"),
                ("master", string.Equals(memberRole, "Master", StringComparison.Ordinal) ? "true" : "false"),
                ("backup", string.Equals(memberRole, "Backup", StringComparison.Ordinal) ? "true" : "false")));
        }

        CanonicalSection identity = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.SystemIdentity,
            ordered: false,
            Props(("name", $"chr-{topologyId}-{memberLabel}"), ("note", "m1-32")));

        CanonicalSection vrrpConfig = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            configRecords.ToArray());

        CanonicalSection vrrpObs = Section(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            obsRecords.ToArray());

        string findingCode = Mode == TopologyMode.SplitMaster ? "VRRP_SPLIT_MASTER" : "VRRP_ACTIVE_PASSIVE";
        string findingSeverity = Mode == TopologyMode.SplitMaster ? "blocker" : "info";
        CanonicalSection topologyFindings = Section(
            CanonicalDomain.Observations,
            CanonicalSectionIds.TopologyValidation,
            ordered: false,
            Props(
                ("code", findingCode),
                ("severity", findingSeverity),
                ("detail", $"topology={topologyId};member={memberLabel};role={memberRole}"),
                ("global-master", "false")));

        List<CanonicalSection> configuration = [identity, vrrpConfig];
        List<CanonicalSection> observations = [vrrpObs, topologyFindings];

        SnapshotHashBundle hashes = CanonicalizationService.HashSnapshotBundle(
            schemaVersion: 1,
            configurationSections: configuration,
            observationSections: observations);

        CanonicalSection capability = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.CapabilitiesDevice,
            ordered: false,
            Props(("profile", "chr-x86_64"), ("topology", topologyId), ("api-ssl", "true"), ("member", memberLabel)));
        CapabilityHash capabilityHash = CapabilityHash.FromDigest(CanonicalHashContract.HashSection(capability));

        byte[] raw = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            topology = topologyId,
            sanitized = true,
            host,
            member = memberLabel,
            role = memberRole,
            mode = Mode.ToString(),
            vrids,
        }));

        // One descriptor per SectionId (config+obs payloads share the same key).
        List<CapturedSectionDescriptor> sections =
        [
            Descriptor(identity, configurationRecords: identity.Records.Count, observationRecords: 0),
            new CapturedSectionDescriptor
            {
                SectionId = CanonicalSectionIds.HaVrrp,
                SectionVersion = 1,
                Status = 1,
                Ordered = false,
                ConfigurationRecordCount = vrrpConfig.Records.Count,
                ObservationRecordCount = vrrpObs.Records.Count,
                ConfigurationPayload = vrrpConfig.Utf8Bytes.ToArray(),
                ObservationPayload = vrrpObs.Utf8Bytes.ToArray(),
            },
            Descriptor(topologyFindings, configurationRecords: 0, observationRecords: topologyFindings.Records.Count),
            Descriptor(capability, configurationRecords: capability.Records.Count, observationRecords: 0),
        ];

        return Task.FromResult(new SnapshotCaptureResult
        {
            ConfigurationHash = hashes.ConfigurationHash,
            ObservationHash = hashes.ObservationHash,
            CapabilityHash = capabilityHash,
            SnapshotHash = hashes.SnapshotHash,
            SchemaVersion = 1,
            RawPayload = raw,
            ConfigurationPayload = ConcatDocuments(configuration.ToArray()),
            ObservationPayload = ConcatDocuments(observations.ToArray()),
            CapabilityPayload = capability.Utf8Bytes.ToArray(),
            Sections = sections,
        });
    }

    private static CanonicalSection Section(
        CanonicalDomain domain,
        string sectionId,
        bool ordered,
        params Dictionary<string, string>[] records)
        => Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = domain,
            SectionId = sectionId,
            Ordered = ordered,
            Records = records.Select(static r => new CanonicalRecordInput { Properties = r }).ToArray(),
        });

    private static Dictionary<string, string> Props(params (string Key, string Value)[] pairs)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string key, string value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    private static CapturedSectionDescriptor Descriptor(
        CanonicalSection section,
        int configurationRecords,
        int observationRecords)
        => new()
        {
            SectionId = section.SectionId,
            SectionVersion = 1,
            Status = 1,
            Ordered = section.Ordered,
            ConfigurationRecordCount = configurationRecords,
            ObservationRecordCount = observationRecords,
            ConfigurationPayload = configurationRecords > 0 ? section.Utf8Bytes.ToArray() : null,
            ObservationPayload = observationRecords > 0 ? section.Utf8Bytes.ToArray() : null,
        };

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
