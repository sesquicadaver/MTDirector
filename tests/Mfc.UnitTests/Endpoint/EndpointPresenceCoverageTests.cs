using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Extra branch coverage for M7.2-02 endpoint presence modules.</summary>
public sealed class EndpointPresenceCoverageTests
{
    private static readonly DateTimeOffset T07 = new(2026, 8, 22, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T08 = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T12 = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T13 = new(2026, 8, 22, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T14 = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    [Fact]
    public void CloseRejectsUntilBeforeValidFrom()
    {
        EndpointPresenceInterval interval = SampleInterval();
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            interval.Close(interval.ValidFrom));
        Assert.Contains(EndpointPresenceCodes.CloseBeforeValidFrom, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconstituteRejectsInvalidValidityRange()
    {
        DateTimeOffset from = T10;
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceInterval.Reconstitute(
                PresenceId.New(),
                EndpointId.New(),
                SiteId.New(),
                NodeId.New(),
                "192.168.1.1",
                EndpointAttributionCertainty.Proven,
                from,
                from));
        Assert.Contains(EndpointPresenceCodes.InvalidValidityRange, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderRejectsMissingSiteId()
    {
        EndpointAttributionResult attribution = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery { Family = "ipv4", IpAddress = "192.168.1.1" },
            new EndpointAttributionSnapshot
            {
                DhcpLeases = [new DhcpLeaseFact { IpAddress = "192.168.1.1", MacAddress = "AA:BB:CC:DD:EE:01" }],
            });
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceBuilder.BuildInterval(
                EndpointId.New(),
                PresenceId.New(),
                attribution,
                new EndpointAttributionQuery { Family = "ipv4", IpAddress = "192.168.1.1" },
                DateTimeOffset.UtcNow));
        Assert.Contains(EndpointPresenceCodes.MissingSiteId, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenRejectsSecondActiveWithoutClosing()
    {
        EndpointId endpointId = EndpointId.New();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointPresenceInterval active = SampleInterval(endpointId, site, node);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceInterval.Open(
                endpointId,
                active,
                Resolve(site, node),
                Query(site, node),
                active.ValidFrom));
        Assert.Contains(EndpointPresenceCodes.InvalidValidityRange, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IntervalContainsRespectsHalfOpenRange()
    {
        EndpointPresenceInterval closed = EndpointPresenceInterval.Reconstitute(
            PresenceId.New(),
            EndpointId.New(),
            SiteId.New(),
            NodeId.New(),
            "192.168.1.1",
            EndpointAttributionCertainty.Proven,
            T08,
            T12);
        Assert.True(closed.Contains(T10));
        Assert.False(closed.Contains(T12));
        Assert.False(closed.Contains(T07));
    }

    [Fact]
    public async Task GetRoutingContextReturnsNotFoundForMissingEndpoint()
    {
        ApplicationResult<EndpointRoutingContextView> missing = await new GetEndpointRoutingContextUseCase(
                new FakeAuthorizationBoundary(),
                new FakeEndpointPresenceStore(),
                new FakeClock())
            .ExecuteAsync(new GetEndpointRoutingContextQuery
            {
                Actor = "tester",
                EndpointId = Guid.NewGuid(),
            });
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task OpenReturnsForbiddenWhenInventoryWriteDenied()
    {
        FakeAuthorizationBoundary denied = new();
        denied.DeniedPermissions.Add(ApplicationPermissions.InventoryWrite);
        ApplicationResult<EndpointRoutingContextView> forbidden = await new OpenEndpointPresenceUseCase(
                denied,
                new FakeEndpointPresenceStore(),
                new FakeClock())
            .ExecuteAsync(new UpsertEndpointPresenceCommand
            {
                Actor = "tester",
                EndpointId = EndpointId.New().Value,
                Query = Query(SiteId.New(), NodeId.New()),
                Snapshot = Snapshot(),
            });
        Assert.Equal("forbidden", forbidden.Error!.Code);
    }

    [Fact]
    public void RoutingContextReconstituteRejectsInvalidValidityRange()
    {
        DateTimeOffset from = T10;
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointRoutingContext.Reconstitute(
                EndpointId.New(),
                PresenceId.New(),
                SiteId.New(),
                NodeId.New(),
                "192.168.1.1",
                from,
                from));
        Assert.Contains(EndpointPresenceCodes.InvalidValidityRange, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CloseOnAlreadyClosedIntervalThrows()
    {
        EndpointPresenceInterval closed = EndpointPresenceInterval.Reconstitute(
            PresenceId.New(),
            EndpointId.New(),
            SiteId.New(),
            NodeId.New(),
            "192.168.1.1",
            EndpointAttributionCertainty.Proven,
            T08,
            T12);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            closed.Close(T13));
        Assert.Contains(EndpointPresenceCodes.IntervalNotActive, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FakeStoreRejectsOverlappingActiveInterval()
    {
        FakeEndpointPresenceStore store = new();
        EndpointPresenceInterval first = SampleInterval();
        EndpointRoutingContext firstContext = EndpointRoutingContextBuilder.Build(first);
        await store.SaveMigrationAsync(null, first, firstContext);
        EndpointPresenceInterval second = SampleInterval(first.EndpointId, first.SiteId, first.NodeId);
        await Assert.ThrowsAsync<DomainInvariantException>(() =>
            store.SaveMigrationAsync(null, second, EndpointRoutingContextBuilder.Build(second)));
    }

    [Fact]
    public void BuilderRejectsMissingNodeId()
    {
        SiteId site = SiteId.New();
        EndpointAttributionResult attribution = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "192.168.1.1",
                SiteId = site,
            },
            new EndpointAttributionSnapshot
            {
                DhcpLeases = [new DhcpLeaseFact { IpAddress = "192.168.1.1", MacAddress = "AA:BB:CC:DD:EE:01" }],
            });
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceBuilder.BuildInterval(
                EndpointId.New(),
                PresenceId.New(),
                attribution,
                new EndpointAttributionQuery
                {
                    Family = "ipv4",
                    IpAddress = "192.168.1.1",
                    SiteId = site,
                },
                T10));
        Assert.Contains(EndpointPresenceCodes.MissingNodeId, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderResolvesInventoryAnchorsFromAttributionHops()
    {
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        DeviceId device = DeviceId.New();
        EndpointAttributionResult attribution = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery { Family = "ipv4", IpAddress = "192.168.1.50" },
            new EndpointAttributionSnapshot
            {
                SiteId = site,
                NodeId = node,
                DeviceId = device,
                DhcpLeases = [new DhcpLeaseFact { IpAddress = "192.168.1.50", MacAddress = "AA:BB:CC:DD:EE:01" }],
            });
        EndpointPresenceInterval interval = EndpointPresenceBuilder.BuildInterval(
            EndpointId.New(),
            PresenceId.New(),
            attribution,
            new EndpointAttributionQuery { Family = "ipv4", IpAddress = "192.168.1.50" },
            T10);
        Assert.Equal(site, interval.SiteId);
        Assert.Equal(node, interval.NodeId);
        Assert.Equal(device, interval.DeviceId);
    }

    [Fact]
    public void OpenWithNoActiveIntervalReturnsOpenedOnly()
    {
        EndpointId endpointId = EndpointId.New();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointPresenceMigrationResult migration = EndpointPresenceInterval.Open(
            endpointId,
            activeInterval: null,
            Resolve(site, node),
            Query(site, node),
            T10);
        Assert.Null(migration.ClosedInterval);
        Assert.Equal(endpointId, migration.OpenedInterval.EndpointId);
        Assert.True(migration.OpenedInterval.IsActive);
    }

    [Fact]
    public void OpenRejectsClosedIntervalMarkedActive()
    {
        EndpointId endpointId = EndpointId.New();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointPresenceInterval closed = EndpointPresenceInterval.Reconstitute(
            PresenceId.New(),
            endpointId,
            site,
            node,
            "192.168.1.1",
            EndpointAttributionCertainty.Proven,
            T08,
            T12);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceInterval.Open(
                endpointId,
                closed,
                Resolve(site, node),
                Query(site, node),
                T14));
        Assert.Contains(EndpointPresenceCodes.OverlappingActiveInterval, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenRejectsEndpointIdMismatch()
    {
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointPresenceInterval active = SampleInterval();
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceInterval.Open(
                EndpointId.New(),
                active,
                Resolve(site, node),
                Query(site, node),
                T14));
        Assert.Contains("endpoint_id mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateRejectsMissingSourceAddress()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointPresenceInterval.Create(
                PresenceId.New(),
                EndpointId.New(),
                SiteId.New(),
                NodeId.New(),
                "  ",
                EndpointAttributionCertainty.Proven,
                T10));
        Assert.Contains(EndpointPresenceCodes.MissingSourceAddress, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveIntervalContainsFuturePoints()
    {
        EndpointPresenceInterval active = SampleInterval();
        Assert.True(active.Contains(T12));
        Assert.False(active.Contains(T07));
    }

    [Fact]
    public void IntervalEqualityCoversValueSemantics()
    {
        PresenceId presenceId = PresenceId.New();
        EndpointId endpointId = EndpointId.New();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        DeviceId device = DeviceId.New();
        EndpointPresenceInterval left = EndpointPresenceInterval.Reconstitute(
            presenceId,
            endpointId,
            site,
            node,
            "192.168.1.50",
            EndpointAttributionCertainty.Proven,
            T10,
            T12,
            device,
            vlanId: "10",
            vrf: "corp",
            macAddress: "AA:BB:CC:DD:EE:01");
        EndpointPresenceInterval right = EndpointPresenceInterval.Reconstitute(
            presenceId,
            endpointId,
            site,
            node,
            "192.168.1.50",
            EndpointAttributionCertainty.Proven,
            T10,
            T12,
            device,
            vlanId: "10",
            vrf: "corp",
            macAddress: "aa:bb:cc:dd:ee:01");
        EndpointPresenceInterval different = EndpointPresenceInterval.Reconstitute(
            PresenceId.New(),
            endpointId,
            site,
            node,
            "192.168.1.50",
            EndpointAttributionCertainty.Proven,
            T10,
            T12);

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.False(left.Equals(null));
        Assert.False(left.Equals(different));
        Assert.False(left.Equals("not-an-interval"));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void RoutingContextEqualityAndContainsCoverActiveInterval()
    {
        EndpointPresenceInterval interval = SampleInterval();
        EndpointRoutingContext left = EndpointRoutingContextBuilder.Build(interval);
        EndpointRoutingContext right = EndpointRoutingContext.Create(interval);
        EndpointRoutingContext different = EndpointRoutingContext.Reconstitute(
            interval.EndpointId,
            PresenceId.New(),
            interval.SiteId,
            interval.NodeId,
            interval.SourceAddress,
            interval.ValidFrom,
            interval.ValidUntil,
            interval.VlanId,
            interval.Vrf);

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.False(left.Equals(null));
        Assert.False(left.Equals(different));
        Assert.False(left.Equals("not-a-context"));
        Assert.True(left.Contains(T12));
        Assert.False(left.Contains(T07));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void RoutingContextBuilderOverloadHonorsValidUntil()
    {
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointRoutingContext context = EndpointRoutingContextBuilder.Build(
            EndpointId.New(),
            PresenceId.New(),
            Resolve(site, node),
            Query(site, node),
            T10,
            validUntil: T12,
            vrf: "corp");
        Assert.Equal(T10, context.ValidFrom);
        Assert.Equal(T12, context.ValidUntil);
        Assert.Equal("corp", context.Vrf);
    }

    private static EndpointPresenceInterval SampleInterval(
        EndpointId? endpointId = null,
        SiteId? siteId = null,
        NodeId? nodeId = null)
    {
        SiteId site = siteId ?? SiteId.New();
        NodeId node = nodeId ?? NodeId.New();
        return EndpointPresenceBuilder.BuildInterval(
            endpointId ?? EndpointId.New(),
            PresenceId.New(),
            Resolve(site, node),
            Query(site, node),
            T10);
    }

    private static EndpointAttributionResult Resolve(SiteId site, NodeId node)
        => EndpointAttributionResolver.Resolve(Query(site, node), Snapshot());

    private static EndpointAttributionQuery Query(SiteId site, NodeId node)
        => new()
        {
            Family = "ipv4",
            IpAddress = "192.168.1.50",
            SiteId = site,
            NodeId = node,
        };

    private static EndpointAttributionSnapshot Snapshot()
        => new()
        {
            DhcpLeases = [new DhcpLeaseFact { IpAddress = "192.168.1.50", MacAddress = "AA:BB:CC:DD:EE:01" }],
            BridgeHostEntries =
            [
                new BridgeHostFact
                {
                    MacAddress = "AA:BB:CC:DD:EE:01",
                    VlanId = "10",
                    Bridge = "br-lan",
                    Port = "ether2",
                },
            ],
        };
}
