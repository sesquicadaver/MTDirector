using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Snapshot;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

/// <summary>Acceptance coverage for menu-specific canonical projection (M1-22).</summary>
public sealed class DiscoveryCanonicalProjectorTests
{
    [Fact]
    public void FirewallRuleOrderIsPreserved()
    {
        CanonicalDeviceSnapshot snapshot = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Firewall = Firewall(
                ipv4Filter:
                [
                    FilterRule(0, 0, "input", "accept"),
                    FilterRule(1, 1, "forward", "drop"),
                    FilterRule(2, 2, "output", "accept"),
                ]),
        });

        CanonicalSection filter = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.FirewallIpv4Filter);
        Assert.True(filter.Ordered);
        Assert.Equal(3, filter.Records.Count);
        Assert.Equal("0", filter.Records[0].Properties["ordinal"]);
        Assert.Equal("input", filter.Records[0].Properties["chain"]);
        Assert.Equal("1", filter.Records[1].Properties["ordinal"]);
        Assert.Equal("forward", filter.Records[1].Properties["chain"]);
        Assert.Equal("2", filter.Records[2].Properties["ordinal"]);
    }

    [Fact]
    public void RouteActiveStateIsSeparatedFromConfiguration()
    {
        CanonicalDeviceSnapshot snapshot = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Routing = Routing(ipv4StaticRoutes: [StaticRoute(active: "true", gatewayStatus: "reachable")]),
        });

        CanonicalSection config = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.RoutingIpv4StaticRoutes);
        Assert.False(config.Records[0].Properties.ContainsKey("active"));
        Assert.False(config.Records[0].Properties.ContainsKey("gateway-status"));
        Assert.Equal("0.0.0.0/0", config.Records[0].Properties["dst-address"]);

        CanonicalSection obs = Assert.Single(
            snapshot.ObservationSections,
            s => s.SectionId == CanonicalSectionIds.RoutingIpv4StaticRoutes);
        Assert.Equal("true", obs.Records[0].Properties["active"]);
        Assert.Equal("reachable", obs.Records[0].Properties["gateway-status"]);
    }

    [Fact]
    public void VrrpRoleIsSeparatedFromConfiguration()
    {
        CanonicalDeviceSnapshot snapshot = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Vrrp = new VrrpDiscoveryResult
            {
                Instances =
                [
                    new VrrpInstanceDiscovery
                    {
                        GroupKey = new VrrpGroupKey(IpAddressFamilyKind.Ipv4, 1, "ether1"),
                        Name = "vr1",
                        ParentInterface = "ether1",
                        Vrid = 1,
                        Family = IpAddressFamilyKind.Ipv4,
                        Priority = 100,
                        IsOwner = false,
                        Version = "3",
                        V3Protocol = "ipv4",
                        Interval = "1s",
                        PreemptionMode = "yes",
                        AuthenticationMode = null,
                        Disabled = "false",
                        Comment = null,
                        VirtualAddresses = ["10.0.0.1"],
                        ObservedRole = VrrpDerivedRole.Master,
                        DomainObservedState = VrrpMemberObservedState.Master,
                        Running = "true",
                        Master = "true",
                        Backup = "false",
                        Failure = null,
                        Invalid = null,
                        RawProperties = EmptyBag(),
                    },
                ],
                Findings = [],
                Warnings = [],
            },
        });

        CanonicalSection config = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.HaVrrp);
        Assert.False(config.Records[0].Properties.ContainsKey("role"));
        Assert.Equal("100", config.Records[0].Properties["priority"]);
        Assert.Equal("ether1", config.Records[0].Properties["interface"]);
        Assert.Equal("1", config.Records[0].Properties["vrid"]);

        CanonicalSection obs = Assert.Single(
            snapshot.ObservationSections,
            s => s.SectionId == CanonicalSectionIds.HaVrrp);
        Assert.Equal("Master", obs.Records[0].Properties["role"]);
    }

    [Fact]
    public void DynamicAddressListEntriesAreObservationsOnly()
    {
        CanonicalDeviceSnapshot snapshot = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Firewall = Firewall(
                ipv4StaticLists:
                [
                    new FirewallAddressListEntryDiscovery
                    {
                        Family = IpAddressFamilyKind.Ipv4,
                        RouterOsRowId = "*1",
                        List = "block",
                        Address = "1.2.3.4",
                        AddressCanonical = "1.2.3.4",
                        Disabled = "false",
                        Comment = null,
                        RawProperties = EmptyBag(),
                    },
                ],
                ipv4DynamicSummaries:
                [
                    new DynamicAddressListSummary
                    {
                        ListName = "dyn",
                        Family = IpAddressFamilyKind.Ipv4,
                        EntryCount = 2,
                        SortedEntryDigestHex = "abc123",
                    },
                ]),
        });

        CanonicalSection config = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.FirewallIpv4AddressLists);
        Assert.DoesNotContain(config.Records, r => r.Properties.ContainsKey("dynamic-digest"));
        Assert.Equal("block", config.Records[0].Properties["list"]);

        CanonicalSection obs = Assert.Single(
            snapshot.ObservationSections,
            s => s.SectionId == CanonicalSectionIds.FirewallIpv4AddressLists);
        Assert.Equal("dyn", obs.Records[0].Properties["list"]);
        Assert.Equal("abc123", obs.Records[0].Properties["dynamic-digest"]);
    }

    [Fact]
    public void InterfaceRunningStateIsObservationsOnly()
    {
        CanonicalDeviceSnapshot snapshot = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Interfaces = Interfaces([Iface("ether1")]),
        });

        CanonicalSection config = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.NetworkInterfaces);
        Assert.False(config.Records[0].Properties.ContainsKey("running"));
        Assert.Equal("ether1", config.Records[0].Properties["name"]);

        CanonicalSection obs = Assert.Single(
            snapshot.ObservationSections,
            s => s.SectionId == CanonicalSectionIds.NetworkInterfaces);
        Assert.Equal("true", obs.Records[0].Properties["running"]);
    }

    [Fact]
    public void UnknownPropertiesDoNotAffectConfigurationHash()
    {
        CanonicalDeviceSnapshot a = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Firewall = Firewall(ipv4Filter: [FilterRule(0, 0, "input", "accept")]),
        });
        CanonicalDeviceSnapshot b = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Firewall = Firewall(ipv4Filter:
            [
                FilterRule(
                    0,
                    0,
                    "input",
                    "accept",
                    raw: new Dictionary<string, string>(StringComparer.Ordinal) { ["weird-matcher"] = "x" }),
            ]),
        });

        Assert.Equal(a.ConfigurationHash.ToString(), b.ConfigurationHash.ToString());
        Assert.NotEqual(a.ObservationHash.ToString(), b.ObservationHash.ToString());
        Assert.Contains(
            b.ObservationSections,
            s => s.SectionId == CanonicalSectionIds.CompatibilityUnknownProperties);
    }

    [Fact]
    public void IdenticalInputsYieldIdenticalHashes()
    {
        DiscoveryCanonicalInput input = new()
        {
            Firewall = Firewall(ipv4Filter: [FilterRule(0, 0, "input", "accept")]),
            Interfaces = Interfaces([Iface("ether1")]),
        };
        CanonicalDeviceSnapshot a = DiscoveryCanonicalProjector.Project(input);
        CanonicalDeviceSnapshot b = DiscoveryCanonicalProjector.Project(input);
        Assert.Equal(a.ConfigurationHash.ToString(), b.ConfigurationHash.ToString());
        Assert.Equal(a.ObservationHash.ToString(), b.ObservationHash.ToString());
        Assert.Equal(a.SnapshotHash.ToString(), b.SnapshotHash.ToString());
    }

    [Fact]
    public void UnorderedSectionsIgnoreApiReplyOrder()
    {
        CanonicalDeviceSnapshot a = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Interfaces = Interfaces([Iface("ether2"), Iface("ether1")]),
        });
        CanonicalDeviceSnapshot b = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Interfaces = Interfaces([Iface("ether1"), Iface("ether2")]),
        });
        Assert.Equal(a.ConfigurationHash.ToString(), b.ConfigurationHash.ToString());
        Assert.Equal(a.ObservationHash.ToString(), b.ObservationHash.ToString());
    }

    [Fact]
    public void ConfigurationChangeChangesConfigurationHash()
    {
        CanonicalDeviceSnapshot before = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Firewall = Firewall(ipv4Filter: [FilterRule(0, 0, "input", "accept")]),
        });
        CanonicalDeviceSnapshot after = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Firewall = Firewall(ipv4Filter: [FilterRule(0, 0, "input", "drop")]),
        });

        Assert.NotEqual(before.ConfigurationHash.ToString(), after.ConfigurationHash.ToString());
    }

    [Fact]
    public void RuntimeChangeChangesOnlyObservationHash()
    {
        CanonicalDeviceSnapshot a = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Interfaces = Interfaces([Iface("ether1", running: "true")]),
        });
        CanonicalDeviceSnapshot b = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            Interfaces = Interfaces([Iface("ether1", running: "false")]),
        });

        Assert.Equal(a.ConfigurationHash.ToString(), b.ConfigurationHash.ToString());
        Assert.NotEqual(a.ObservationHash.ToString(), b.ObservationHash.ToString());
        Assert.NotEqual(a.SnapshotHash.ToString(), b.SnapshotHash.ToString());
    }

    [Fact]
    public void PacketPathTopologyEmitsContainerVethAndSharedVethSections()
    {
        // AC-J: projector emits LOCK-2 sections from discovery fixtures.
        PacketPathTopologyResult topology = PacketPathTopologyDiscovery.BuildResult(
            containers: Ok(
                RosReadCommandId.Containers,
                Row(("name", "pihole"), ("interface", "veth1"), ("status", "running")),
                Row(("name", "pg"), ("interface", "veth1"), ("status", "stopped"))),
            apps: Ok(
                RosReadCommandId.Apps,
                Row(("name", "store"), ("interface", "veth2"), ("running", "true"))),
            vethInterfaces: Ok(
                RosReadCommandId.VethInterfaces,
                Row(("name", "veth1"), ("running", "true")),
                Row(("name", "veth2"), ("running", "true"))),
            vlanInterfaces: Ok(RosReadCommandId.VlanInterfaces),
            bridges: EmptyBridges(),
            vrfs: Ok(RosReadCommandId.IpVrfs));

        CanonicalDeviceSnapshot snapshot = DiscoveryCanonicalProjector.Project(new DiscoveryCanonicalInput
        {
            PacketPathTopology = topology,
        });

        CanonicalSection containerVeth = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.TopologyContainerVeth);
        Assert.False(containerVeth.Ordered);
        Assert.Contains(
            containerVeth.Records,
            r => r.Properties["endpoint_kind"] == "container"
                 && r.Properties["endpoint_name"] == "pihole"
                 && r.Properties["veth_name"] == "veth1");
        Assert.Contains(
            containerVeth.Records,
            r => r.Properties["endpoint_kind"] == "container"
                 && r.Properties["endpoint_name"] == "pg"
                 && r.Properties["veth_name"] == "veth1");
        Assert.Contains(
            containerVeth.Records,
            r => r.Properties["endpoint_kind"] == "app"
                 && r.Properties["endpoint_name"] == "store"
                 && r.Properties["veth_name"] == "veth2");

        CanonicalSection shared = Assert.Single(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.TopologySharedVeth);
        Assert.Contains(shared.Records, r => r.Properties["veth_name"] == "veth1");
        Assert.DoesNotContain(
            snapshot.ConfigurationSections,
            s => s.SectionId == CanonicalSectionIds.TopologyValidation);
        Assert.DoesNotContain(
            snapshot.ObservationSections,
            s => s.SectionId is CanonicalSectionIds.TopologyContainerVeth
                or CanonicalSectionIds.TopologySharedVeth);
    }

    private static BridgeSwitchDiscoveryResult EmptyBridges()
        => BridgeSwitchDiscovery.BuildResult(
            Ok(RosReadCommandId.Bridges),
            Ok(RosReadCommandId.BridgePorts),
            Ok(RosReadCommandId.BridgeSettings),
            Ok(RosReadCommandId.BridgeVlans),
            Ok(RosReadCommandId.EthernetSwitches),
            Ok(RosReadCommandId.EthernetSwitchPorts));

    private static RosReadCommandResult Ok(RosReadCommandId id, params RosReadRecord[] rows)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static FirewallFilterRuleDiscovery FilterRule(
        int effective,
        int staticOrdinal,
        string chain,
        string action,
        IReadOnlyDictionary<string, string>? raw = null)
        => new()
        {
            Family = IpAddressFamilyKind.Ipv4,
            RouterOsRowId = $"*{effective + 1}",
            EffectiveOrdinal = effective,
            StaticOrdinal = staticOrdinal,
            IsDynamic = false,
            Chain = chain,
            Action = action,
            Disabled = "false",
            Comment = null,
            FwcOwnershipMarker = null,
            HasFwcOwnershipMarker = false,
            Protocol = null,
            SrcAddress = null,
            DstAddress = null,
            ConnectionState = null,
            HwOffload = null,
            JumpTarget = null,
            RejectWith = null,
            AddressList = null,
            AddressListTimeout = null,
            Invalid = null,
            KnownProperties = EmptyBag(),
            RawProperties = raw ?? EmptyBag(),
        };

    private static FirewallFilterDiscoveryResult Firewall(
        IReadOnlyList<FirewallFilterRuleDiscovery>? ipv4Filter = null,
        IReadOnlyList<FirewallAddressListEntryDiscovery>? ipv4StaticLists = null,
        IReadOnlyList<DynamicAddressListSummary>? ipv4DynamicSummaries = null)
        => new()
        {
            Ipv4FilterRules = ipv4Filter ?? [],
            Ipv6FilterRules = [],
            Ipv4StaticAddressListEntries = ipv4StaticLists ?? [],
            Ipv6StaticAddressListEntries = [],
            Ipv4DynamicAddressListSummaries = ipv4DynamicSummaries ?? [],
            Ipv6DynamicAddressListSummaries = [],
            Warnings = [],
        };

    private static InterfaceDiscovery Iface(string name, string running = "true")
        => new()
        {
            Id = $"*{name}",
            Name = name,
            DefaultName = name,
            Type = "ether",
            Mtu = "1500",
            MacAddress = "00:11:22:33:44:55",
            Disabled = "false",
            Comment = null,
            Running = running,
            ActualMtu = "1500",
            Dynamic = "false",
            Slave = "false",
            RawProperties = EmptyBag(),
        };

    private static InterfaceAddressDiscoveryResult Interfaces(IReadOnlyList<InterfaceDiscovery> interfaces)
        => new()
        {
            Interfaces = interfaces,
            Ipv4StaticAddresses = [],
            Ipv4DynamicAddresses = [],
            Ipv6StaticAddresses = [],
            Ipv6DynamicAddresses = [],
            InterfaceLists = [],
            InterfaceListMembers = [],
            ResolvedMembership = [],
            Findings = [],
            Warnings = [],
        };

    private static StaticRouteDiscovery StaticRoute(string active, string gatewayStatus)
        => new()
        {
            Family = IpAddressFamilyKind.Ipv4,
            DstAddress = "0.0.0.0/0",
            Gateway = "10.0.0.1",
            RoutingTable = "main",
            Distance = 1,
            Scope = 30,
            TargetScope = 10,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
            Comment = null,
            IsDynamic = false,
            Active = active,
            ImmediateGateway = "10.0.0.1%ether1",
            GatewayStatus = gatewayStatus,
            RawProperties = EmptyBag(),
        };

    private static RoutingDependencyDiscoveryResult Routing(
        IReadOnlyList<StaticRouteDiscovery>? ipv4StaticRoutes = null)
        => new()
        {
            RoutingTables = [],
            RoutingRules = [],
            Ipv4StaticRoutes = ipv4StaticRoutes ?? [],
            Ipv6StaticRoutes = [],
            Ipv4DefaultRouteState = [],
            Ipv6DefaultRouteState = [],
            Ipv4NatRules = [],
            Ipv6NatRules = [],
            Ipv4RawRules = [],
            Ipv6RawRules = [],
            Ipv4MangleRules = [],
            Ipv6MangleRules = [],
            Ipv4Settings = new Ipv4SettingsDiscovery
            {
                IpForward = "true",
                RpFilter = "no",
                AcceptSourceRoute = null,
                AllowFastPath = null,
                TcpSyncookies = null,
                Ipv4FasttrackActive = null,
                RawProperties = EmptyBag(),
            },
            Ipv6Settings = new Ipv6SettingsDiscovery
            {
                Forward = "true",
                DisableIpv6 = "false",
                AcceptRouterAdvertisements = null,
                RawProperties = EmptyBag(),
            },
            Findings = [],
            Warnings = [],
        };

    private static Dictionary<string, string> EmptyBag()
        => new(StringComparer.Ordinal);
}
