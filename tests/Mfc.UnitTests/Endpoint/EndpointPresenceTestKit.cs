using Mfc.Application.Abstractions.Time;
using Mfc.Application.Endpoint;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;

namespace Mfc.UnitTests.Endpoint;

internal static class EndpointPresenceTestKit
{
    public static OpenEndpointPresenceUseCase CreateOpenUseCase(
        FakeAuthorizationBoundary? auth = null,
        FakeEndpointPresenceStore? presence = null,
        FakeResponseAssessmentStore? assessments = null,
        FakeRoutingAssuranceStateStore? routing = null,
        FakeClock? clock = null)
        => new(
            auth ?? new FakeAuthorizationBoundary(),
            presence ?? new FakeEndpointPresenceStore(),
            assessments ?? new FakeResponseAssessmentStore(),
            routing ?? new FakeRoutingAssuranceStateStore(),
            clock ?? new FakeClock());

    public static UpsertEndpointPresenceCommand Command(
        EndpointId endpointId,
        SiteId site,
        NodeId node,
        string ip = "192.168.1.50",
        string vlan = "10",
        string? vrf = "corp",
        RouteResolutionTrace? corporateRouteTrace = null,
        DeviceId? mobilityDeviceId = null,
        EndpointMobilityProbeTargets? probeTargets = null)
        => new()
        {
            Actor = "tester",
            EndpointId = endpointId.Value,
            Query = new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = ip,
                SiteId = site,
                NodeId = node,
            },
            Snapshot = new EndpointAttributionSnapshot
            {
                DhcpLeases = [new DhcpLeaseFact { IpAddress = ip, MacAddress = "AA:BB:CC:DD:EE:01" }],
                BridgeHostEntries =
                [
                    new BridgeHostFact
                    {
                        MacAddress = "AA:BB:CC:DD:EE:01",
                        VlanId = vlan,
                        Bridge = "br-lan",
                        Port = "ether2",
                    },
                ],
            },
            CorporateRouteTrace = corporateRouteTrace,
            Vrf = vrf,
            MobilityRoutingDeviceId = mobilityDeviceId?.Value,
            MobilityProbeTargets = probeTargets,
        };
}
