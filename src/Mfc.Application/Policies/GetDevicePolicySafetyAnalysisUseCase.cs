using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Snapshots;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

/// <summary>Read-only query: last capture + optional revision → ManagementPath/FastTrack facts (W5-02).</summary>
public sealed class GetDevicePolicySafetyAnalysisQuery
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }

    /// <summary>When set, FastTrack analyzes that revision's rules; otherwise an empty rule list.</summary>
    public Guid? RevisionId { get; init; }

    /// <summary>Required controller source CIDRs. Never invented as 0.0.0.0/0.</summary>
    public IReadOnlyList<string> ControllerSourcePrefixes { get; init; } = [];
}

/// <summary>
/// Runs existing M2-13 / M2-15 mappers against the device's last completed capture.
/// Does not write RouterOS, does not invent VRRP roles, and does not recompute on Desktop.
/// </summary>
public sealed class GetDevicePolicySafetyAnalysisUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly INodeStore _nodes;
    private readonly ISnapshotStore _snapshots;
    private readonly IPolicyStore _policies;

    public GetDevicePolicySafetyAnalysisUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        INodeStore nodes,
        ISnapshotStore snapshots,
        IPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(policies);
        _auth = auth;
        _devices = devices;
        _nodes = nodes;
        _snapshots = snapshots;
        _policies = policies;
    }

    public async Task<ApplicationResult<PolicySafetyAnalysisView>> ExecuteAsync(
        GetDevicePolicySafetyAnalysisQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationResult<IReadOnlyList<AddressPrefix>> prefixesResult = ParseControllerPrefixes(
            query.ControllerSourcePrefixes);
        if (prefixesResult.IsFailure)
        {
            return ApplicationResults.Fail(prefixesResult.Error!);
        }

        Device? device = await _devices.GetAsync(new DeviceId(query.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Device '{query.DeviceId}' not found."));
        }

        if (device.LastCompletedCaptureId is not Guid captureId)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound(
                    $"Device '{query.DeviceId}' has no completed capture for policy safety analysis."));
        }

        Node? node = await _nodes.GetAsync(device.NodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{device.NodeId.Value}' not found."));
        }

        IReadOnlyList<CanonicalSection> sections = await _snapshots
            .LoadCanonicalSectionsAsync(new SnapshotId(captureId), cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PolicyRule> fastTrackRules = [];
        if (query.RevisionId is Guid revisionId)
        {
            (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
                .LoadRevisionAsync(_policies, revisionId, cancellationToken)
                .ConfigureAwait(false);
            if (loadError is not null)
            {
                return ApplicationResults.Fail(loadError);
            }

            ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision!);
            if (document.IsFailure)
            {
                return ApplicationResults.Fail(document.Error!);
            }

            fastTrackRules = document.Value!.Rules;
        }

        IReadOnlyList<Device> members = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);

        ManagementAccessProfile profile;
        try
        {
            profile = ManagementAccessProfile.Create(
                prefixesResult.Value!,
                device.ManagementEndpoint.Host.Value,
                device.ManagementEndpoint.Port,
                physicalManagementAddresses: PhysicalAddresses(device));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        IReadOnlyList<CanonicalRecord> ipServices = Records(
            sections, CanonicalSectionIds.ManagementIpServices, CanonicalDomain.Configuration);
        IReadOnlyList<CanonicalRecord> ipv4Filter = Records(
            sections, CanonicalSectionIds.FirewallIpv4Filter, CanonicalDomain.Configuration);
        IReadOnlyList<CanonicalRecord> ipv6Filter = Records(
            sections, CanonicalSectionIds.FirewallIpv6Filter, CanonicalDomain.Configuration);

        ManagementPathAnalysisResult management = ManagementPathContextMapper.Analyze(
            profile,
            ipServices,
            ipv4Filter,
            ipv6Filter);

        TopologyDependencyProfile topologyProfile = TopologyDependencyProfile.Create(
            kind: node.DeclaredKind,
            uplinkMode: node.DeclaredUplinkMode,
            declaredVrrpMemberIds: node.DeclaredKind == NodeKind.Vrrp
                ? members.Select(static d => d.Id.Value.ToString("D")).ToArray()
                : [],
            observingDeviceId: device.Id.Value.ToString("D"));

        FastTrackAnalysisResult fastTrack = FastTrackContextMapper.Analyze(
            fastTrackRules,
            topologyProfile,
            ToTopologySections(sections),
            ipv4Filter,
            PacketPathNodes(sections));

        return ApplicationResults.Ok(ToView(query.DeviceId, captureId, query.RevisionId, management, fastTrack));
    }

    private static ApplicationResult<IReadOnlyList<AddressPrefix>> ParseControllerPrefixes(
        IReadOnlyList<string> rawPrefixes)
    {
        List<AddressPrefix> parsed = [];
        foreach (string raw in rawPrefixes)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            try
            {
                parsed.Add(AddressPrefix.Parse(raw.Trim()));
            }
            catch (DomainInvariantException ex)
            {
                return ApplicationResults.Fail(
                    ApplicationError.Validation($"Invalid controller_source_prefix '{raw.Trim()}': {ex.Message}"));
            }
        }

        if (parsed.Count == 0)
        {
            return ApplicationResults.Fail(
                ApplicationError.Validation(
                    "controller_source_prefixes is required; analysis does not invent a default prefix."));
        }

        return ApplicationResults.Ok<IReadOnlyList<AddressPrefix>>(parsed);
    }

    private static IReadOnlyList<string> PhysicalAddresses(Device device)
    {
        HostNameOrIp host = device.ManagementEndpoint.Host;
        return host.HostKind is HostNameOrIp.Kind.IPv4 or HostNameOrIp.Kind.IPv6
            ? [host.Value]
            : [];
    }

    private static IReadOnlyList<CanonicalRecord> Records(
        IReadOnlyList<CanonicalSection> sections,
        string sectionId,
        CanonicalDomain domain)
    {
        foreach (CanonicalSection section in sections)
        {
            if (section.SectionId == sectionId && section.Domain == domain)
            {
                return section.Records;
            }
        }

        return [];
    }

    private static List<CanonicalRecord> PacketPathNodes(IReadOnlyList<CanonicalSection> sections)
    {
        List<CanonicalRecord> nodes = [];
        foreach (CanonicalSection section in sections)
        {
            if (section.SectionId is CanonicalSectionIds.TopologyValidation
                or CanonicalSectionIds.TopologyContainerVeth)
            {
                nodes.AddRange(section.Records);
            }
        }

        return nodes;
    }

    private static TopologyDependencyCanonicalSections ToTopologySections(IReadOnlyList<CanonicalSection> sections)
        => new()
        {
            VrrpConfiguration = Records(sections, CanonicalSectionIds.HaVrrp, CanonicalDomain.Configuration),
            VrrpObservations = Records(sections, CanonicalSectionIds.HaVrrp, CanonicalDomain.Observations),
            RoutingTables = Records(sections, CanonicalSectionIds.RoutingTables, CanonicalDomain.Configuration),
            RoutingRules = Records(sections, CanonicalSectionIds.RoutingRules, CanonicalDomain.Configuration),
            Ipv4Nat = Records(sections, CanonicalSectionIds.FirewallIpv4Nat, CanonicalDomain.Configuration),
            Ipv6Nat = Records(sections, CanonicalSectionIds.FirewallIpv6Nat, CanonicalDomain.Configuration),
            Ipv4Raw = Records(sections, CanonicalSectionIds.FirewallIpv4Raw, CanonicalDomain.Configuration),
            Ipv6Raw = Records(sections, CanonicalSectionIds.FirewallIpv6Raw, CanonicalDomain.Configuration),
            Ipv4Mangle = Records(sections, CanonicalSectionIds.FirewallIpv4Mangle, CanonicalDomain.Configuration),
            Ipv6Mangle = Records(sections, CanonicalSectionIds.FirewallIpv6Mangle, CanonicalDomain.Configuration),
            Ipv4Settings = Records(sections, CanonicalSectionIds.NetworkIpv4Settings, CanonicalDomain.Configuration),
            Ipv4DefaultState = Records(
                sections, CanonicalSectionIds.RoutingIpv4DefaultState, CanonicalDomain.Configuration),
            Ipv6DefaultState = Records(
                sections, CanonicalSectionIds.RoutingIpv6DefaultState, CanonicalDomain.Configuration),
            SwitchInstances = Records(sections, CanonicalSectionIds.SwitchInstances, CanonicalDomain.Configuration),
            BridgeSettings = Records(sections, CanonicalSectionIds.BridgeSettings, CanonicalDomain.Configuration),
        };

    private static PolicySafetyAnalysisView ToView(
        Guid deviceId,
        Guid captureId,
        Guid? revisionId,
        ManagementPathAnalysisResult management,
        FastTrackAnalysisResult fastTrack)
        => new()
        {
            DeviceId = deviceId,
            CaptureId = captureId,
            RevisionId = revisionId,
            ManagementPathContextHashHex = management.ManagementPathContextHash.ToString(),
            FastTrackContextHashHex = fastTrack.FastTrackContextHash.ToString(),
            BlocksManagementPath = management.BlocksManagementPath,
            AllowsSafeFastTrack = fastTrack.AllowsSafeFastTrack,
            RequiresAcceptFallback = fastTrack.RequiresAcceptFallback,
            RiskFloor = fastTrack.RiskFloor ?? string.Empty,
            ManagementPathFindings = management.Findings.Select(ToFinding).ToArray(),
            FastTrackFindings = fastTrack.Findings.Select(ToFinding).ToArray(),
            SystemTests = management.SystemTests.Select(ToSystemTest).ToArray(),
        };

    private static PolicySafetyFindingView ToFinding(ManagementPathFinding finding)
        => new()
        {
            Code = finding.Code,
            Severity = finding.Severity,
            Message = finding.Message,
            Subject = finding.Chain is null && finding.Ordinal is null
                ? null
                : $"{finding.Chain}:{finding.Ordinal}",
            Witness = finding.Witness is null ? null : ToWitness(finding.Witness),
        };

    private static PolicySafetyFindingView ToFinding(FastTrackFinding finding)
        => new()
        {
            Code = finding.Code,
            Severity = finding.Severity,
            Message = finding.Message,
            Subject = finding.Subject,
        };

    private static ManagementSystemTestView ToSystemTest(ManagementSystemTest test)
        => new()
        {
            Origin = test.Origin,
            Chain = test.Chain,
            Expected = test.Expected,
            Packet = ToWitness(test.Packet),
        };

    private static PolicyWitnessPacketView ToWitness(PolicyWitnessPacket packet)
        => new()
        {
            Family = packet.Family,
            Chain = packet.Chain,
            SourceAddress = packet.SourceAddress,
            DestinationAddress = packet.DestinationAddress,
            Protocol = packet.Protocol,
            SourcePort = packet.SourcePort,
            DestinationPort = packet.DestinationPort,
            ConnectionState = packet.ConnectionState?.ToString(),
        };
}
