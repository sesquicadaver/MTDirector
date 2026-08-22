using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Living Spec matrix for Issue Set M7.2-02 AC (endpoint presence + routing context).</summary>
public sealed class EndpointPresenceLivingSpecTests
{
    private static readonly DateTimeOffset T08 = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T12 = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T14 = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T15 = new(2026, 8, 22, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T16 = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T17 = new(2026, 8, 22, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T18 = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Ac1BuildPresenceIntervalFromAttributionResult()
    {
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointAttributionResult attribution = ResolveLanAttribution(site, node, vlanId: "10", vrf: "corp");

        EndpointPresenceInterval interval = EndpointPresenceBuilder.BuildInterval(
            EndpointId.New(),
            PresenceId.New(),
            attribution,
            LanQuery(site, node, "192.168.1.50"),
            T10,
            vrf: "corp");

        Assert.Equal(site, interval.SiteId);
        Assert.Equal(node, interval.NodeId);
        Assert.Equal("10", interval.VlanId);
        Assert.Equal("corp", interval.Vrf);
        Assert.Equal("192.168.1.50", interval.SourceAddress);
        Assert.Equal("aa:bb:cc:dd:ee:01", interval.MacAddress);
    }

    [Fact]
    public void Ac2RoutingContextStoresCorporateInternetAndWazuhTraces()
    {
        EndpointPresenceInterval interval = SampleInterval();
        RouteResolutionTrace corporate = Trace("10.20.0.10", "corp", "ether2");
        RouteResolutionTrace internet = Trace("203.0.113.10", "main", "ether1");
        RouteResolutionTrace wazuh = Trace("10.50.0.5", "corp", "vlan50");

        EndpointRoutingContext context = EndpointRoutingContextBuilder.Build(
            interval,
            corporate,
            internet,
            wazuh);

        Assert.Equal(corporate.DestinationAddress, context.CorporateRouteTrace!.DestinationAddress);
        Assert.Equal(internet.DestinationAddress, context.InternetRouteTrace!.DestinationAddress);
        Assert.Equal(wazuh.DestinationAddress, context.WazuhRouteTrace!.DestinationAddress);
    }

    [Fact]
    public void Ac3ActivePresenceHasValidFromAndNullValidUntil()
    {
        DateTimeOffset validFrom = T12;
        EndpointPresenceInterval interval = SampleInterval(validFrom);

        Assert.Equal(validFrom, interval.ValidFrom);
        Assert.Null(interval.ValidUntil);
        Assert.True(interval.IsActive);
    }

    [Fact]
    public void Ac4MigrationClosesPreviousIntervalAndOpensNewPresence()
    {
        EndpointId endpointId = EndpointId.New();
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        DateTimeOffset firstFrom = T08;
        DateTimeOffset secondFrom = T14;

        EndpointPresenceInterval first = EndpointPresenceBuilder.BuildInterval(
            endpointId,
            PresenceId.New(),
            ResolveLanAttribution(siteA, nodeA, vlanId: "10"),
            LanQuery(siteA, nodeA, "192.168.1.50"),
            firstFrom);
        EndpointPresenceMigrationResult migration = EndpointPresenceInterval.Open(
            endpointId,
            first,
            ResolveLanAttribution(siteB, nodeB, vlanId: "20"),
            LanQuery(siteB, nodeB, "192.168.2.50"),
            secondFrom,
            vrf: "guest");

        Assert.NotNull(migration.ClosedInterval);
        Assert.Equal(secondFrom, migration.ClosedInterval!.ValidUntil);
        Assert.NotEqual(first.PresenceId, migration.OpenedInterval.PresenceId);
        Assert.Equal(siteB, migration.OpenedInterval.SiteId);
        Assert.Equal("20", migration.OpenedInterval.VlanId);
        Assert.Null(migration.OpenedInterval.ValidUntil);
    }

    [Fact]
    public async Task Ac5PersistenceRoundTripStoresPresenceAndRoutingContext()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore store = new();
        FakeClock clock = new() { UtcNow = T15 };
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointId endpointId = EndpointId.New();
        OpenEndpointPresenceUseCase open = new(auth, store, clock);

        ApplicationResult<EndpointRoutingContextView> written = await open.ExecuteAsync(
            new UpsertEndpointPresenceCommand
            {
                Actor = "tester",
                EndpointId = endpointId.Value,
                Query = LanQuery(site, node, "192.168.1.50"),
                Snapshot = LanSnapshot(),
                CorporateRouteTrace = Trace("10.20.0.10", "corp", "ether2"),
                InternetRouteTrace = Trace("203.0.113.10", "main", "ether1"),
                WazuhRouteTrace = Trace("10.50.0.5", "corp", "vlan50"),
                Vrf = "corp",
            });
        Assert.True(written.IsSuccess, written.Error?.Message);

        EndpointRoutingContext? persisted = await store.GetRoutingContextAsync(new PresenceId(written.Value!.PresenceId));
        Assert.NotNull(persisted);
        Assert.Equal("10.20.0.10", persisted!.CorporateRouteTrace!.DestinationAddress);
        Assert.Equal("corp", persisted.Vrf);
        EndpointPresenceInterval? active = await store.GetActiveIntervalAsync(endpointId);
        Assert.NotNull(active);
        Assert.Equal(EndpointAttributionCertainty.Proven, active!.AttributionCertainty);
    }

    [Fact]
    public async Task Ac6AttributionCertaintyPreservedOnPresence()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore store = new();
        FakeClock clock = new();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        OpenEndpointPresenceUseCase open = new(auth, store, clock);

        ApplicationResult<EndpointRoutingContextView> partial = await open.ExecuteAsync(
            new UpsertEndpointPresenceCommand
            {
                Actor = "tester",
                EndpointId = EndpointId.New().Value,
                Query = LanQuery(site, node, "192.168.1.60"),
                Snapshot = new EndpointAttributionSnapshot
                {
                    DhcpLeases =
                    [
                        new DhcpLeaseFact { IpAddress = "192.168.1.60", MacAddress = "AA:BB:CC:DD:EE:01" },
                    ],
                    ArpEntries =
                    [
                        new ArpFact { IpAddress = "192.168.1.60", MacAddress = "AA:BB:CC:DD:EE:02" },
                    ],
                },
            });
        Assert.True(partial.IsSuccess);

        EndpointPresenceInterval? active = await store.GetActiveIntervalAsync(new EndpointId(partial.Value!.EndpointId));
        Assert.Equal(EndpointAttributionCertainty.Partial, active!.AttributionCertainty);
    }

    [Fact]
    public void Ac7NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/firewall/filter/add"));
        Assert.DoesNotContain(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs.Write", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac8AsOfQueryReturnsCorrectActiveInterval()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore store = new();
        FakeClock clock = new() { UtcNow = T16 };
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        EndpointId endpointId = EndpointId.New();
        OpenEndpointPresenceUseCase open = new(auth, store, clock);
        GetEndpointRoutingContextUseCase get = new(auth, store, clock);

        await open.ExecuteAsync(new UpsertEndpointPresenceCommand
        {
            Actor = "tester",
            EndpointId = endpointId.Value,
            Query = LanQuery(siteA, nodeA, "192.168.1.50"),
            Snapshot = LanSnapshot(),
            Vrf = "corp",
        });
        clock.UtcNow = T18;
        await open.ExecuteAsync(new UpsertEndpointPresenceCommand
        {
            Actor = "tester",
            EndpointId = endpointId.Value,
            Query = LanQuery(siteB, nodeB, "192.168.2.50"),
            Snapshot = LanSnapshot(mac: "AA:BB:CC:DD:EE:02", ip: "192.168.2.50", vlan: "20"),
            Vrf = "guest",
        });

        ApplicationResult<EndpointRoutingContextView> historical = await get.ExecuteAsync(
            new GetEndpointRoutingContextQuery
            {
                Actor = "tester",
                EndpointId = endpointId.Value,
                AsOfUtc = T17,
            });
        Assert.True(historical.IsSuccess);
        Assert.Equal(siteA.Value, historical.Value!.SiteId);
        Assert.Equal("corp", historical.Value.Vrf);

        ApplicationResult<EndpointRoutingContextView> current = await get.ExecuteAsync(
            new GetEndpointRoutingContextQuery
            {
                Actor = "tester",
                EndpointId = endpointId.Value,
            });
        Assert.True(current.IsSuccess);
        Assert.Equal(siteB.Value, current.Value!.SiteId);
        Assert.Equal("guest", current.Value.Vrf);
    }

    private static EndpointPresenceInterval SampleInterval(DateTimeOffset? validFrom = null)
    {
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        return EndpointPresenceBuilder.BuildInterval(
            EndpointId.New(),
            PresenceId.New(),
            ResolveLanAttribution(site, node, vlanId: "10"),
            LanQuery(site, node, "192.168.1.50"),
            validFrom ?? T12,
            vrf: "corp");
    }

    private static EndpointAttributionQuery LanQuery(SiteId site, NodeId node, string ip)
        => new()
        {
            Family = "ipv4",
            IpAddress = ip,
            SiteId = site,
            NodeId = node,
        };

    private static EndpointAttributionSnapshot LanSnapshot(
        string ip = "192.168.1.50",
        string mac = "AA:BB:CC:DD:EE:01",
        string vlan = "10")
        => new()
        {
            DhcpLeases = [new DhcpLeaseFact { IpAddress = ip, MacAddress = mac }],
            BridgeHostEntries =
            [
                new BridgeHostFact
                {
                    MacAddress = mac,
                    VlanId = vlan,
                    Bridge = "br-lan",
                    Port = "ether2",
                },
            ],
        };

    private static EndpointAttributionResult ResolveLanAttribution(
        SiteId site,
        NodeId node,
        string vlanId,
        string? vrf = null)
        => EndpointAttributionResolver.Resolve(
            LanQuery(site, node, "192.168.1.50"),
            LanSnapshot(vlan: vlanId));

    private static RouteResolutionTrace Trace(string destination, string table, string egress)
        => new()
        {
            Family = "ipv4",
            SourceAddress = "192.168.1.50",
            DestinationAddress = destination,
            SelectedTable = table,
            SelectedVrf = table == "corp" ? "corp" : null,
            Decision = RouteResolutionDecisions.Forward,
            EgressInterfaces = [egress],
            Certainty = RouteResolutionCertainties.Definite,
        };
}
