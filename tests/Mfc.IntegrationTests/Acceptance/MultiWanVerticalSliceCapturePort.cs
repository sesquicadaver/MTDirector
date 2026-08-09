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
/// Lab-only capture port for multi-WAN failover/balanced topologies (M1-31).
/// Emits routing/NAT/mangle/default-state sections without product write commands.
/// </summary>
public sealed class MultiWanVerticalSliceCapturePort : ISnapshotCapturePort
{
    public enum WanMode
    {
        Failover,
        Balanced,
    }

    private int _captureCount;

    public int CaptureCount => Volatile.Read(ref _captureCount);

    public WanMode Mode { get; set; } = WanMode.Failover;

    /// <summary>Static default route gateway (configuration).</summary>
    public string StaticRouteGateway { get; set; } = "10.255.21.1";

    /// <summary>Active flag on default route observation (does not affect configuration hash).</summary>
    public string DefaultRouteActive { get; set; } = "true";

    public string RpFilter { get; set; } = "strict";

    public RouterOsReadTarget? LastTarget { get; private set; }

    public Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _captureCount);
        LastTarget = target;

        string topologyId = Mode == WanMode.Failover ? "multi-wan-failover" : "multi-wan-balanced";
        string wan1Role = Mode == WanMode.Failover ? "primary" : "balanced";
        string wan2Role = Mode == WanMode.Failover ? "secondary" : "balanced";
        string wan2Gw = Mode == WanMode.Failover ? "10.255.22.1" : "10.255.32.1";

        CanonicalSection identity = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.SystemIdentity,
            ordered: false,
            Props(("name", $"chr-{topologyId}"), ("note", "m1-31")));

        CanonicalSection tables = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.RoutingTables,
            ordered: false,
            Props(("name", "main"), ("fib", "true")),
            Props(("name", "wan1"), ("fib", "true"), ("uplink-role", wan1Role)),
            Props(("name", "wan2"), ("fib", "true"), ("uplink-role", wan2Role)));

        CanonicalSection rules = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.RoutingRules,
            ordered: true,
            Props(("src-address", "10.0.0.0/8"), ("action", "lookup"), ("table", "wan1"), ("uplink-role", wan1Role)),
            Props(("src-address", "10.0.0.0/8"), ("action", "lookup"), ("table", "wan2"), ("uplink-role", wan2Role)));

        CanonicalSection staticRoutes = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.RoutingIpv4StaticRoutes,
            ordered: false,
            Props(
                ("dst-address", "0.0.0.0/0"),
                ("gateway", StaticRouteGateway),
                ("routing-table", "wan1"),
                ("uplink-role", wan1Role),
                ("distance", "1")),
            Props(
                ("dst-address", "0.0.0.0/0"),
                ("gateway", wan2Gw),
                ("routing-table", "wan2"),
                ("uplink-role", wan2Role),
                ("distance", Mode == WanMode.Failover ? "2" : "1")));

        CanonicalSection defaultState = Section(
            CanonicalDomain.Observations,
            CanonicalSectionIds.RoutingIpv4DefaultState,
            ordered: false,
            Props(
                ("dst-address", "0.0.0.0/0"),
                ("gateway", StaticRouteGateway),
                ("active", DefaultRouteActive),
                ("routing-table", "wan1"),
                ("uplink-role", wan1Role)));

        CanonicalSection nat = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Nat,
            ordered: true,
            Props(("chain", "srcnat"), ("action", "masquerade"), ("out-interface", "wan1"), ("uplink-role", wan1Role)),
            Props(("chain", "srcnat"), ("action", "masquerade"), ("out-interface", "wan2"), ("uplink-role", wan2Role)));

        CanonicalSection mangle = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Mangle,
            ordered: true,
            Props(
                ("chain", "prerouting"),
                ("action", "mark-routing"),
                ("new-routing-mark", "wan1"),
                ("passthrough", "true"),
                ("uplink-role", wan1Role),
                ("pcc", Mode == WanMode.Balanced ? "both-addresses:2/0" : "none")),
            Props(
                ("chain", "prerouting"),
                ("action", "mark-routing"),
                ("new-routing-mark", "wan2"),
                ("passthrough", "true"),
                ("uplink-role", wan2Role),
                ("pcc", Mode == WanMode.Balanced ? "both-addresses:2/1" : "none")));

        CanonicalSection ipv4Settings = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.NetworkIpv4Settings,
            ordered: false,
            Props(("rp-filter", RpFilter), ("ip-forward", "true")));

        CanonicalSection topologyFindings = Section(
            CanonicalDomain.Observations,
            CanonicalSectionIds.TopologyValidation,
            ordered: false,
            Props(
                ("code", RpFilter == "strict" ? "STRICT_RP_FILTER" : "RP_FILTER_OK"),
                ("severity", RpFilter == "strict" ? "warning" : "info"),
                ("detail", $"rp-filter={RpFilter}")));

        List<CanonicalSection> configuration =
        [
            identity,
            tables,
            rules,
            staticRoutes,
            nat,
            mangle,
            ipv4Settings,
        ];
        List<CanonicalSection> observations = [defaultState, topologyFindings];

        SnapshotHashBundle hashes = CanonicalizationService.HashSnapshotBundle(
            schemaVersion: 1,
            configurationSections: configuration,
            observationSections: observations);

        CanonicalSection capability = Section(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.CapabilitiesDevice,
            ordered: false,
            Props(("profile", "chr-x86_64"), ("topology", topologyId), ("api-ssl", "true")));
        CapabilityHash capabilityHash = CapabilityHash.FromDigest(CanonicalHashContract.HashSection(capability));

        byte[] raw = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            topology = topologyId,
            sanitized = true,
            mode = Mode.ToString(),
            staticRouteGateway = StaticRouteGateway,
            defaultRouteActive = DefaultRouteActive,
            rpFilter = RpFilter,
            wan1Role,
            wan2Role,
        }));

        List<CapturedSectionDescriptor> sections = [];
        foreach (CanonicalSection section in configuration.Append(capability))
        {
            sections.Add(Descriptor(section, configurationRecords: section.Records.Count, observationRecords: 0));
        }

        foreach (CanonicalSection section in observations)
        {
            sections.Add(Descriptor(section, configurationRecords: 0, observationRecords: section.Records.Count));
        }

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
