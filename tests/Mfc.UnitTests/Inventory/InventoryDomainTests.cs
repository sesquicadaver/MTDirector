using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Xunit;

namespace Mfc.UnitTests.Inventory;

public sealed class SiteTests
{
    [Fact]
    public void CreateAcceptsValidCodeAndKeepsDraft()
    {
        Site site = Site.Create(SiteCode.Create("DC1"), NonEmptyName.Create("Primary DC"));
        Assert.Equal(SiteStatus.Draft, site.Status);
        Assert.Equal("DC1", site.Code.Value);
        Assert.Equal(1UL, site.RowVersion);
        Assert.Equal("DC1", site.Code.ToString());
        Assert.True(site.Code.Equals(SiteCode.Create("DC1")));
        Assert.True(site.Code == SiteCode.Create("DC1"));
        Assert.False(site.Code != SiteCode.Create("DC1"));
        Assert.False(site.Code.Equals(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("dc1")]
    [InlineData("1DC")]
    [InlineData("A")]
    [InlineData("THIS_CODE_IS_WAY_TOO_LONG_FOR_SITE")]
    public void SiteCodeRejectsInvalidValues(string code)
    {
        Assert.ThrowsAny<Exception>(() => SiteCode.Create(code));
    }

    [Fact]
    public void CodeCannotChangeAfterActivate()
    {
        Site site = Site.Create(SiteCode.Create("EDGE01"), NonEmptyName.Create("Edge"));
        site.Activate();
        Assert.Equal(SiteStatus.Active, site.Status);

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => site.ChangeCode(SiteCode.Create("EDGE02")));
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("EDGE01", site.Code.Value);
    }

    [Fact]
    public void CodeCanChangeWhileDraftAndRenameDisableWork()
    {
        Site site = Site.Create(SiteCode.Create("TMP"), NonEmptyName.Create("Temp"));
        site.ChangeCode(SiteCode.Create("OK_SITE"));
        site.Rename(NonEmptyName.Create("Renamed"));
        Assert.Equal("OK_SITE", site.Code.Value);
        Assert.Equal("Renamed", site.Name.Value);

        site.Disable();
        Assert.Equal(SiteStatus.Disabled, site.Status);
        Assert.Throws<DomainInvariantException>(() => site.Activate());
    }
}

public sealed class NodeDeviceInvariantTests
{
    [Fact]
    public void RouterNodeRejectsSecondDevice()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("r1"),
            NodeKind.Router,
            DeclaredUplinkMode.One);

        node.AddDevice(
            NonEmptyName.Create("r1-dev"),
            ManagementEndpoint.Create("10.0.0.1"),
            DeviceRole.Router);

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            node.AddDevice(
                NonEmptyName.Create("r1-dev-2"),
                ManagementEndpoint.Create("10.0.0.2"),
                DeviceRole.Router));

        Assert.Contains("more than one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SwitchNodeAlsoRejectsSecondDevice()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("sw1"),
            NodeKind.Switch,
            DeclaredUplinkMode.None);

        node.AddDevice(
            NonEmptyName.Create("sw1-dev"),
            ManagementEndpoint.Create("10.0.0.5"),
            DeviceRole.L2Switch);

        Assert.Throws<DomainInvariantException>(() =>
            node.AddDevice(
                NonEmptyName.Create("sw1-dev-2"),
                ManagementEndpoint.Create("10.0.0.6"),
                DeviceRole.L2Switch));
    }

    [Fact]
    public void VrrpNodeCannotActivateWithFewerThanTwoDevices()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("vrrp1"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.Failover);

        node.AddDevice(
            NonEmptyName.Create("m1"),
            ManagementEndpoint.Create("10.0.1.1"),
            DeviceRole.Router);

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() => node.Activate());
        Assert.Contains("at least two", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NodeStatus.Draft, node.Status);
        Assert.False(node.SatisfiesActiveDeviceCardinality());
    }

    [Fact]
    public void VrrpNodeActivatesWithTwoDevicesAndSupportsLifecycle()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("vrrp1"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.Failover);

        node.Rename(NonEmptyName.Create("vrrp-core"));
        node.SetDeclaredUplinkMode(DeclaredUplinkMode.Balanced);
        Device first = node.AddDevice(NonEmptyName.Create("m1"), ManagementEndpoint.Create("10.0.1.1"), DeviceRole.Router);
        node.AddDevice(NonEmptyName.Create("m2"), ManagementEndpoint.Create("10.0.1.2"), DeviceRole.Router);
        Assert.True(node.SatisfiesActiveDeviceCardinality());
        node.Activate();

        Assert.Equal(NodeStatus.Active, node.Status);
        Assert.Equal(2, node.Devices.Count);
        Assert.Equal(DeclaredUplinkMode.Balanced, node.DeclaredUplinkMode);

        first.Rename(NonEmptyName.Create("m1-renamed"));
        first.Relocate(ManagementEndpoint.Create("10.0.1.11", 8729));
        first.SetRole(DeviceRole.Unknown);
        first.SetEnabled(false);
        first.RecordSupportState(SupportState.ReadOnly);
        Assert.Equal("m1-renamed", first.DisplayName.Value);
        Assert.False(first.Enabled);
        Assert.Equal(SupportState.ReadOnly, first.LastSupportState);

        node.Disable();
        Assert.Equal(NodeStatus.Disabled, node.Status);
        Assert.Throws<DomainInvariantException>(() => node.Activate());
    }

    [Fact]
    public void ActiveRouterCannotDropBelowOneDeviceOrChangeKindInvalidly()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("r1"),
            NodeKind.Router,
            DeclaredUplinkMode.None);

        Device device = node.AddDevice(
            NonEmptyName.Create("r1-dev"),
            ManagementEndpoint.Create("192.168.1.1"),
            DeviceRole.Router);
        node.Activate();

        Assert.Throws<DomainInvariantException>(() => node.RemoveDevice(device.Id));
        Assert.Throws<DomainInvariantException>(() => node.SetDeclaredKind(NodeKind.Vrrp));
        Assert.Throws<DomainInvariantException>(() => node.RemoveDevice(DeviceId.New()));

        node.SetDeclaredKind(NodeKind.Router);
        Assert.Equal(NodeKind.Router, node.DeclaredKind);
    }
}

public sealed class ManagementEndpointTests
{
    [Fact]
    public void AcceptsIpv4Ipv6AndDnsHost()
    {
        HostNameOrIp v4 = HostNameOrIp.Create("10.1.2.3");
        HostNameOrIp v6 = HostNameOrIp.Create("2001:db8::1");
        HostNameOrIp dns = HostNameOrIp.Create("Router.Lab.Local.");
        Assert.Equal(HostNameOrIp.Kind.IPv4, v4.HostKind);
        Assert.Equal(HostNameOrIp.Kind.IPv6, v6.HostKind);
        Assert.Equal(HostNameOrIp.Kind.DnsHostName, dns.HostKind);
        Assert.Equal("router.lab.local", dns.Value);
        Assert.True(v4.Equals(HostNameOrIp.Create("10.1.2.3")));
        Assert.Equal("10.1.2.3", v4.ToString());

        ManagementEndpoint endpoint = ManagementEndpoint.Create("10.1.2.3");
        Assert.Equal(ManagementEndpoint.DefaultApiSslPort, endpoint.Port);
        Assert.True(endpoint.Equals(ManagementEndpoint.Create(HostNameOrIp.Create("10.1.2.3"))));
        Assert.Contains("10.1.2.3", endpoint.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a host!!")]
    [InlineData("-bad.example")]
    [InlineData("")]
    public void RejectsArbitraryStrings(string host)
    {
        Assert.ThrowsAny<Exception>(() => HostNameOrIp.Create(host));
    }

    [Fact]
    public void RejectsPortZero()
    {
        Assert.Throws<DomainInvariantException>(
            () => ManagementEndpoint.Create(HostNameOrIp.Create("10.0.0.1"), port: 0));
    }

    [Fact]
    public void AddressPrefixAndHashHelpersWork()
    {
        AddressPrefix prefix = AddressPrefix.Parse("10.0.0.1/24");
        Assert.Equal(IpAddressFamily.IPv4, prefix.Family);
        Assert.Equal("10.0.0.1/24", prefix.ToString());
        Assert.True(prefix.Equals(AddressPrefix.Create(IPAddress.Parse("10.0.0.1"), 24)));
        Assert.Throws<DomainInvariantException>(() => AddressPrefix.Parse("nope"));
        Assert.Throws<DomainInvariantException>(() => AddressPrefix.Create(IPAddress.Parse("10.0.0.1"), 40));

        Hash256 hash = Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray());
        Assert.Equal(64, hash.ToString().Length);
        Assert.True(hash.Equals(Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray())));
        Assert.Throws<DomainInvariantException>(() => Hash256.Create(new byte[8]));
    }

    [Fact]
    public void NonEmptyNameBounds()
    {
        Assert.Equal("ok", NonEmptyName.Create(" ok ").Value);
        Assert.ThrowsAny<Exception>(() => NonEmptyName.Create(" "));
        Assert.Throws<DomainInvariantException>(() => NonEmptyName.Create(new string('a', 129)));
        NonEmptyName name = NonEmptyName.Create("x");
        Assert.True(name.Equals(NonEmptyName.Create("x")));
        Assert.Equal("x", name.ToString());
    }
}

public sealed class UplinkZoneVrrpTests
{
    [Fact]
    public void UplinkStoresTypedSourceAddressNotString()
    {
        Uplink uplink = Uplink.Create(
            NodeId.New(),
            NonEmptyName.Create("wan1"),
            UplinkTrafficMode.Primary,
            NonEmptyName.Create("WAN_PRIMARY"),
            routingTable: "main",
            sourceAddress: IPAddress.Parse("203.0.113.10"));

        Assert.Equal(IPAddress.Parse("203.0.113.10"), uplink.SourceAddress);
        Assert.Equal(UplinkTrafficMode.Primary, uplink.Mode);
        uplink.SetMode(UplinkTrafficMode.Backup);
        uplink.SetZoneKey(NonEmptyName.Create("WAN_BACKUP"));
        uplink.SetSourceAddress(null);
        Assert.Null(uplink.SourceAddress);
        Assert.Throws<DomainInvariantException>(() =>
            Uplink.Create(NodeId.New(), NonEmptyName.Create("w"), UplinkTrafficMode.Transit, NonEmptyName.Create("Z"), routingTable: "  "));
    }

    [Fact]
    public void ZoneBindingRequiresValuesAndHash()
    {
        Hash256 hash = Hash256.Create(Enumerable.Repeat((byte)7, 32).ToArray());
        ZoneBinding binding = ZoneBinding.Create(
            NodeId.New(),
            NonEmptyName.Create("LAN"),
            ZoneAddressFamily.IPv4,
            ZoneBindingType.InterfaceList,
            ["LAN"],
            ["ether1", "bridge-lan"],
            hash);

        Assert.Equal(2, binding.ResolvedMembers.Count);
        Assert.Equal(hash, binding.DependencyHash);
        Hash256 next = Hash256.Create(Enumerable.Repeat((byte)8, 32).ToArray());
        binding.ReplaceResolvedMembers(["ether2"], next);
        Assert.Equal("ether2", Assert.Single(binding.ResolvedMembers));
        Assert.Throws<DomainInvariantException>(() =>
            ZoneBinding.Create(NodeId.New(), NonEmptyName.Create("Z"), ZoneAddressFamily.Dual, ZoneBindingType.InterfaceSet, [], [], hash));
        Assert.Throws<DomainInvariantException>(() =>
            ZoneBinding.Create(NodeId.New(), NonEmptyName.Create("Z"), ZoneAddressFamily.Dual, ZoneBindingType.InterfaceSet, [" "], [], hash));
    }

    [Fact]
    public void VrrpGroupRejectsFamilyMismatchAndTracksPerMemberState()
    {
        VrrpGroup group = VrrpGroup.Create(
            NodeId.New(),
            IpAddressFamily.IPv4,
            vrid: 10,
            NonEmptyName.Create("ether1"),
            [AddressPrefix.Parse("10.10.10.1/24")],
            TimeSpan.FromSeconds(1),
            preemption: true);

        Assert.Throws<DomainInvariantException>(() =>
            VrrpGroup.Create(
                NodeId.New(),
                IpAddressFamily.IPv4,
                vrid: 11,
                NonEmptyName.Create("ether1"),
                [AddressPrefix.Parse("2001:db8::1/64")],
                TimeSpan.FromSeconds(1),
                preemption: false));

        Assert.Throws<DomainInvariantException>(() =>
            VrrpGroup.Create(
                NodeId.New(),
                IpAddressFamily.IPv4,
                vrid: 0,
                NonEmptyName.Create("ether1"),
                [AddressPrefix.Parse("10.10.10.1/24")],
                TimeSpan.FromSeconds(1),
                preemption: false));

        DeviceId d1 = DeviceId.New();
        DeviceId d2 = DeviceId.New();
        VrrpMember m1 = group.AddMember(d1, configuredPriority: 200, configuredOwner: false);
        VrrpMember m2 = group.AddMember(d2, configuredPriority: 100, configuredOwner: false);
        Assert.Throws<DomainInvariantException>(() => group.AddMember(d1, 50, false));
        Assert.Throws<DomainInvariantException>(() => group.AddMember(DeviceId.New(), 0, false));

        m1.Configure(180, owner: true);
        m1.RecordObservation(VrrpMemberObservedState.Master, DateTimeOffset.UtcNow);
        m2.RecordObservation(VrrpMemberObservedState.Backup, DateTimeOffset.UtcNow);
        // Explicit non-zero offset: DateTimeOffset.Now is UTC on CI runners and would not throw.
        Assert.Throws<DomainInvariantException>(() =>
            m2.RecordObservation(
                VrrpMemberObservedState.Init,
                new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.FromHours(3))));

        group.SetPreemption(false);
        group.SetAdvertisementInterval(TimeSpan.FromMilliseconds(500));
        Assert.Throws<DomainInvariantException>(() => group.SetAdvertisementInterval(TimeSpan.Zero));

        Assert.Equal(VrrpMemberObservedState.Master, m1.ObservedState);
        Assert.Equal(VrrpMemberObservedState.Backup, m2.ObservedState);
        Assert.Equal(2, group.Members.Count);
        Assert.False(group.Preemption);
    }
}

public sealed class InventoryEnumSurfaceTests
{
    [Fact]
    public void ClosedEnumsExposeExpectedMembers()
    {
        Assert.Equal(3, Enum.GetValues<NodeKind>().Length);
        Assert.Equal(5, Enum.GetValues<DeclaredUplinkMode>().Length);
        Assert.Equal(4, Enum.GetValues<DeviceRole>().Length);
        Assert.NotNull(typeof(AssemblyMarker).Assembly);
        Assert.Equal(typeof(Site), AssemblyMarker.InventoryAnchor);
        Assert.False(string.IsNullOrWhiteSpace(SiteId.New().ToString()));
        Assert.False(string.IsNullOrWhiteSpace(NodeId.New().ToString()));
        Assert.False(string.IsNullOrWhiteSpace(DeviceId.New().ToString()));
        Assert.False(string.IsNullOrWhiteSpace(UplinkId.New().ToString()));
        Assert.False(string.IsNullOrWhiteSpace(VrrpGroupId.New().ToString()));
        Assert.False(string.IsNullOrWhiteSpace(ZoneBindingId.New().ToString()));
    }
}
