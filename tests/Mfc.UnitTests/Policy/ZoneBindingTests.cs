using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ZoneDefinitionTests
{
    [Fact]
    public void CompanyZoneRejectsOwnerId()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ZoneDefinition.Create(
                PolicyOwnerScope.Company,
                Guid.NewGuid(),
                NonEmptyName.Create("mgmt"),
                NonEmptyName.Create("Management")));
    }

    [Fact]
    public void SiteZoneRequiresOwnerIdAndSupportsRenameConcurrency()
    {
        Guid siteId = Guid.NewGuid();
        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Site,
            siteId,
            NonEmptyName.Create("lan"),
            NonEmptyName.Create("LAN"));
        Assert.Equal(1UL, zone.RowVersion);
        zone.Rename(NonEmptyName.Create("LAN Core"));
        Assert.Equal(2UL, zone.RowVersion);
        Assert.Equal("LAN Core", zone.Name.Value);
    }
}

public sealed class NodeZoneBindingTests
{
    [Fact]
    public void SingleInterfaceRequiresExactlyOneValue()
    {
        Assert.Throws<DomainInvariantException>(() =>
            NodeZoneBinding.Create(
                new NodeId(Guid.NewGuid()),
                ZoneId.New(),
                NodeZoneBindingKind.SingleInterface,
                ["ether1", "ether2"],
                Hash256.Create(new byte[32])));
    }

    [Fact]
    public void RecordResolveMarksAnalysisStaleOnHashMismatch()
    {
        Hash256 expected = Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray());
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            expected);
        Assert.True(binding.AnalysisStale);

        Hash256 fresh = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            ["ether1"]);
        binding.RecordResolve(fresh);
        Assert.True(binding.AnalysisStale);
        Assert.Equal(fresh, binding.LastResolvedDependencyHash);

        binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            fresh);
        binding.RecordResolve(fresh);
        Assert.False(binding.AnalysisStale);
    }
}

public sealed class ZoneResolveEngineTests
{
    [Fact]
    public void MissingDynamicAndEmptyProduceBlockersPerDevice()
    {
        ZoneId zoneId = ZoneId.New();
        NodeId nodeId = new(Guid.NewGuid());
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            ["ether1", "ether2"]);
        NodeZoneBinding binding = NodeZoneBinding.Create(
            nodeId,
            zoneId,
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            expected);

        DeviceId deviceA = new(Guid.NewGuid());
        DeviceId deviceB = new(Guid.NewGuid());

        ZoneBindingResolveResult a = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = deviceA,
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                    new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = true },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });

        Assert.Contains(a.Blockers, b => b.Code == ZoneResolveBlockerCodes.DynamicInterface);
        Assert.Equal(["ether1"], a.ResolvedMembers);
        Assert.True(a.AnalysisStale);

        ZoneBindingResolveResult b = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = deviceB,
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "sfp1", Dynamic = false },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });

        Assert.Contains(b.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingInterface);
        Assert.Contains(b.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
        Assert.Empty(b.ResolvedMembers);
    }

    [Fact]
    public void InterfaceListIncludeExcludeHonoredAndVrrpNamesMayDiffer()
    {
        ZoneId zoneId = ZoneId.New();
        Hash256 expected = Hash256.Create(new byte[32]);
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            zoneId,
            NodeZoneBindingKind.InterfaceList,
            ["LAN"],
            expected);

        ZoneResolveDeviceObservation member1 = new()
        {
            DeviceId = new DeviceId(Guid.NewGuid()),
            ObservationAvailable = true,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "ether3", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "ether4", Dynamic = false },
            ],
            InterfaceLists =
            [
                new InterfaceListSpec { Name = "LAN", Include = ["ACCESS"], Exclude = ["GUEST"] },
                new InterfaceListSpec { Name = "ACCESS", Include = [], Exclude = [] },
                new InterfaceListSpec { Name = "GUEST", Include = [], Exclude = [] },
            ],
            InterfaceListMembers =
            [
                new InterfaceListMemberSpec { List = "ACCESS", Interface = "ether2", Disabled = false },
                new InterfaceListMemberSpec { List = "ACCESS", Interface = "ether3", Disabled = false },
                new InterfaceListMemberSpec { List = "GUEST", Interface = "ether3", Disabled = false },
                new InterfaceListMemberSpec { List = "LAN", Interface = "ether4", Disabled = false },
            ],
        };

        ZoneResolveDeviceObservation member2 = new()
        {
            DeviceId = new DeviceId(Guid.NewGuid()),
            ObservationAvailable = true,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "sfp-sfpplus1", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "sfp-sfpplus2", Dynamic = false },
            ],
            InterfaceLists =
            [
                new InterfaceListSpec { Name = "LAN", Include = [], Exclude = [] },
            ],
            InterfaceListMembers =
            [
                new InterfaceListMemberSpec { List = "LAN", Interface = "sfp-sfpplus1", Disabled = false },
                new InterfaceListMemberSpec { List = "LAN", Interface = "sfp-sfpplus2", Disabled = false },
            ],
        };

        ZoneBindingResolveResult r1 = ZoneResolveEngine.Resolve(binding, member1);
        ZoneBindingResolveResult r2 = ZoneResolveEngine.Resolve(binding, member2);

        Assert.Equal(["ether2", "ether4"], r1.ResolvedMembers);
        Assert.Equal(["sfp-sfpplus1", "sfp-sfpplus2"], r2.ResolvedMembers);
        Assert.DoesNotContain(r1.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
        Assert.DoesNotContain(r2.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void ObservationUnavailableProducesTypedBlocker()
    {
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = false,
                Interfaces = [],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });

        Assert.Contains(result.Blockers, b => b.Code == ZoneResolveBlockerCodes.ObservationUnavailable);
        Assert.True(result.AnalysisStale);
    }

    [Fact]
    public void InterfaceListCycleAndMissingListProduceTypedBlockers()
    {
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.InterfaceList,
            ["LAN"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult cycle = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                ],
                InterfaceLists =
                [
                    new InterfaceListSpec { Name = "LAN", Include = ["WAN"], Exclude = [] },
                    new InterfaceListSpec { Name = "WAN", Include = ["LAN"], Exclude = [] },
                ],
                InterfaceListMembers =
                [
                    new InterfaceListMemberSpec { List = "LAN", Interface = "ether1", Disabled = false },
                ],
            });
        Assert.Contains(cycle.Blockers, b => b.Code == ZoneResolveBlockerCodes.InterfaceListCycle);

        ZoneBindingResolveResult missing = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });
        Assert.Contains(missing.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingInterfaceList);
        Assert.Contains(missing.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void PlainVlanBridgeVethNamesResolveViaInterfaceTable()
    {
        // AC-A: plain vlan/bridge/veth names still resolve via IF table.
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["vlan120", "bridge-lan", "veth1"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces:
                [
                    Iface("vlan120"),
                    Iface("bridge-lan"),
                    Iface("veth1"),
                ]));

        Assert.Equal(["bridge-lan", "veth1", "vlan120"], result.ResolvedMembers);
        Assert.DoesNotContain(result.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void ContainerMarkerExpandsToVethSet()
    {
        // AC-B
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["container:pihole"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("veth1"), Iface("veth2")],
                edges:
                [
                    Edge("container", "pihole", "veth1"),
                    Edge("container", "pihole", "veth2"),
                ]));

        Assert.Equal(["veth1", "veth2"], result.ResolvedMembers);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public void AppMarkerExpandsToVethSet()
    {
        // AC-C
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["app:store"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("veth-app")],
                edges: [Edge("app", "store", "veth-app")]));

        Assert.Equal(["veth-app"], result.ResolvedMembers);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public void EmptyMarkerRemainderProducesTypedMissingBlocker()
    {
        // Architect must-fix: empty container:/app: remainder
        NodeZoneBinding emptyContainer = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["container:"],
            Hash256.Create(new byte[32]));
        ZoneBindingResolveResult containerResult = ZoneResolveEngine.Resolve(
            emptyContainer,
            Observation(interfaces: [Iface("veth1")], edges: [Edge("container", "x", "veth1")]));
        Assert.Contains(
            containerResult.Blockers,
            b => b.Code == ZoneResolveBlockerCodes.MissingContainer
                 && string.Equals(b.Subject, "container:", StringComparison.Ordinal));
        Assert.DoesNotContain(
            containerResult.Blockers,
            b => b.Code == ZoneResolveBlockerCodes.MissingInterface
                 && string.Equals(b.Subject, "container:", StringComparison.Ordinal));

        NodeZoneBinding emptyApp = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["app:   "],
            Hash256.Create(new byte[32]));
        Assert.Equal(["app:"], emptyApp.Values);
        ZoneBindingResolveResult appResult = ZoneResolveEngine.Resolve(
            emptyApp,
            Observation(interfaces: [Iface("veth1")], edges: [Edge("app", "x", "veth1")]));
        Assert.Contains(
            appResult.Blockers,
            b => b.Code == ZoneResolveBlockerCodes.MissingApp
                 && string.Equals(b.Subject, "app:", StringComparison.Ordinal)
                 && b.Message.Contains("empty name", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingContainerAndAppProduceTypedBlockers()
    {
        // AC-D
        NodeZoneBinding containerBinding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["container:missing"],
            Hash256.Create(new byte[32]));
        ZoneBindingResolveResult missingContainer = ZoneResolveEngine.Resolve(
            containerBinding,
            Observation(interfaces: [Iface("ether1")]));
        Assert.Contains(missingContainer.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingContainer);
        Assert.DoesNotContain(
            missingContainer.Blockers,
            b => b.Code == ZoneResolveBlockerCodes.MissingInterface
                 && string.Equals(b.Subject, "container:missing", StringComparison.Ordinal));
        Assert.Contains(missingContainer.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);

        NodeZoneBinding appBinding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["app:missing"],
            Hash256.Create(new byte[32]));
        ZoneBindingResolveResult missingApp = ZoneResolveEngine.Resolve(
            appBinding,
            Observation(interfaces: [Iface("ether1")]));
        Assert.Contains(missingApp.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingApp);
        Assert.Contains(missingApp.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void UnresolvedVethAfterExpansionProducesTypedBlocker()
    {
        // AC-E
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["container:pihole"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult emptyVeth = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("ether1")],
                edges: [Edge("container", "pihole", "   ")]));
        Assert.Contains(emptyVeth.Blockers, b => b.Code == ZoneResolveBlockerCodes.ContainerVethUnresolved);
        Assert.Contains(emptyVeth.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
        Assert.Empty(emptyVeth.ResolvedMembers);

        ZoneBindingResolveResult missingIf = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("ether1")],
                edges: [Edge("container", "pihole", "veth-gone")]));
        Assert.Contains(missingIf.Blockers, b => b.Code == ZoneResolveBlockerCodes.ContainerVethUnresolved);
        Assert.Contains(missingIf.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingInterface);
        Assert.Contains(missingIf.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);

        NodeZoneBinding appBinding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["app:store"],
            Hash256.Create(new byte[32]));
        ZoneBindingResolveResult appUnresolved = ZoneResolveEngine.Resolve(
            appBinding,
            Observation(
                interfaces: [Iface("ether1")],
                edges: [Edge("app", "store", "veth-gone")]));
        Assert.Contains(appUnresolved.Blockers, b => b.Code == ZoneResolveBlockerCodes.AppVethUnresolved);
    }

    [Fact]
    public void SharedVethProducesBlockerButKeepsResolvedMembers()
    {
        // AC-F / LOCK-5
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["container:pihole"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("veth1")],
                edges: [Edge("container", "pihole", "veth1")],
                shared: ["veth1"]));

        Assert.Equal(["veth1"], result.ResolvedMembers);
        Assert.Contains(result.Blockers, b => b.Code == ZoneResolveBlockerCodes.SharedVeth && b.Subject == "veth1");
        Assert.DoesNotContain(result.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void MarkerOnInterfaceListProducesTypedBlockerWithoutExpansion()
    {
        // AC-G
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.InterfaceList,
            ["container:pihole"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("veth1")],
                edges: [Edge("container", "pihole", "veth1")],
                lists:
                [
                    new InterfaceListSpec { Name = "LAN", Include = [], Exclude = [] },
                ],
                members:
                [
                    new InterfaceListMemberSpec { List = "LAN", Interface = "veth1", Disabled = false },
                ]));

        Assert.Contains(
            result.Blockers,
            b => b.Code == ZoneResolveBlockerCodes.MarkerNotAllowedOnInterfaceList);
        Assert.Empty(result.ResolvedMembers);
        Assert.Contains(result.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void DependencyHashV1UsesMarkersAndPostExpansionMembers()
    {
        // AC-H3 / LOCK-6 — hash prefix stays mfc.zone.dependency.v1
        string[] values = ["container:pihole"];
        string[] members = ["veth1", "veth2"];
        Hash256 a = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            values,
            members);
        Hash256 b = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            values,
            members);
        Assert.Equal(a, b);

        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            values,
            a);

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            Observation(
                interfaces: [Iface("veth1"), Iface("veth2")],
                edges:
                [
                    Edge("container", "pihole", "veth1"),
                    Edge("container", "pihole", "veth2"),
                ]));

        Assert.Equal(a, result.FreshDependencyHash);
        Assert.False(result.AnalysisStale);
        Assert.Equal(NodeZoneBinding.DependencyHashPrefix, "mfc.zone.dependency.v1");
    }

    private static ZoneResolveDeviceObservation Observation(
        IReadOnlyList<ZoneResolveInterfaceObservation>? interfaces = null,
        IReadOnlyList<ZoneResolveContainerVethEdge>? edges = null,
        IReadOnlyList<string>? shared = null,
        IReadOnlyList<InterfaceListSpec>? lists = null,
        IReadOnlyList<InterfaceListMemberSpec>? members = null)
        => new()
        {
            DeviceId = new DeviceId(Guid.NewGuid()),
            ObservationAvailable = true,
            Interfaces = interfaces ?? [],
            InterfaceLists = lists ?? [],
            InterfaceListMembers = members ?? [],
            ContainerVethEdges = edges ?? [],
            SharedVethNames = shared ?? [],
        };

    private static ZoneResolveInterfaceObservation Iface(string name, bool dynamic = false)
        => new() { Name = name, Dynamic = dynamic };

    private static ZoneResolveContainerVethEdge Edge(string kind, string endpoint, string veth)
        => new() { EndpointKind = kind, EndpointName = endpoint, VethName = veth };
}

public sealed class ZoneDefinitionOwnerScopeTests
{
    [Fact]
    public void NodeScopeAndReconstituteValidateOwners()
    {
        Guid nodeId = Guid.NewGuid();
        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Node,
            nodeId,
            NonEmptyName.Create("guest"),
            NonEmptyName.Create("Guest"),
            "  note  ");
        Assert.Equal("note", zone.Description);
        zone.SetDescription(null);
        Assert.Null(zone.Description);

        ZoneDefinition reconstituted = ZoneDefinition.Reconstitute(
            zone.Id,
            zone.OwnerScope,
            zone.OwnerId,
            zone.Key,
            zone.Name,
            "again",
            zone.RowVersion);
        Assert.Equal("again", reconstituted.Description);

        Assert.Throws<DomainInvariantException>(() =>
            ZoneDefinition.Create(
                PolicyOwnerScope.Node,
                null,
                NonEmptyName.Create("x"),
                NonEmptyName.Create("X")));
        Assert.Throws<DomainInvariantException>(() =>
            ZoneDefinition.Reconstitute(
                ZoneId.New(),
                PolicyOwnerScope.Company,
                null,
                NonEmptyName.Create("x"),
                NonEmptyName.Create("X"),
                null,
                rowVersion: 0));
    }
}
