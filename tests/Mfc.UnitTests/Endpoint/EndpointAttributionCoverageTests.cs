using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Extra branch coverage for M7.2-01 endpoint attribution modules.</summary>
public sealed class EndpointAttributionCoverageTests
{
    [Fact]
    public void ResolveRejectsEmptyIpAddress()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            EndpointAttributionResolver.Resolve(
                new EndpointAttributionQuery
                {
                    Family = "ipv4",
                    IpAddress = "  ",
                },
                new EndpointAttributionSnapshot()));
        Assert.Contains("IP address", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedFamilyReturnsUnknownWithFinding()
    {
        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipx",
                IpAddress = "1.2.3.4",
            },
            new EndpointAttributionSnapshot());

        Assert.Equal(EndpointAttributionCertainty.Unknown, result.Certainty);
        Assert.Equal(EndpointAttributionCodes.UnsupportedFamily, Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void MacNormalizationResolvesCompactLeaseMacAgainstColonArpMac()
    {
        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "192.168.1.77",
            },
            new EndpointAttributionSnapshot
            {
                DhcpLeases =
                [
                    new DhcpLeaseFact
                    {
                        IpAddress = "192.168.1.77",
                        MacAddress = "AABBCCDDEE77",
                    },
                ],
                ArpEntries =
                [
                    new ArpFact
                    {
                        IpAddress = "192.168.1.77",
                        MacAddress = "aa:bb:cc:dd:ee:77",
                        Interface = "ether1",
                    },
                ],
            });

        Assert.Equal(EndpointAttributionCertainty.Partial, result.Certainty);
        Assert.Equal("aa:bb:cc:dd:ee:77", result.Chain.Hops.First(h => h.Kind == EndpointAttributionHopKind.Mac).Value);
    }

    [Fact]
    public void SnoopingFallbackResolvesVlanWhenBridgeHostMissing()
    {
        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "10.0.0.5",
            },
            new EndpointAttributionSnapshot
            {
                DhcpSnoopingBindings =
                [
                    new DhcpSnoopingBindingFact
                    {
                        IpAddress = "10.0.0.5",
                        MacAddress = "AA:BB:CC:DD:EE:05",
                        VlanId = "30",
                        Bridge = "br-guest",
                        Port = "ether5",
                    },
                ],
            });

        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Vlan && h.Value == "30");
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Bridge && h.Value == "br-guest");
    }

    [Fact]
    public void IpsecAndPppSessionsMapThroughSnapshotMapper()
    {
        Dictionary<RosReadCommandId, RosReadCommandResult> reads = new()
        {
            [RosReadCommandId.IpsecActivePeers] = Records(
                RosReadCommandId.IpsecActivePeers,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["local-address"] = "10.9.0.2",
                    ["remote-address"] = "203.0.113.5",
                    ["state"] = "established",
                }),
            [RosReadCommandId.PppActiveSessions] = Records(
                RosReadCommandId.PppActiveSessions,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = "pppoe-client",
                    ["address"] = "10.10.10.2",
                    ["caller-id"] = "user@corp",
                }),
        };

        EndpointAttributionSnapshot snapshot = EndpointAttributionSnapshotMapper.Map(reads);
        Assert.Equal(2, snapshot.VpnSessions.Count);

        EndpointAttributionResult ipsec = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery { Family = "ipv4", IpAddress = "10.9.0.2" },
            new EndpointAttributionSnapshot
            {
                ArpEntries = [new ArpFact { IpAddress = "10.9.0.2", MacAddress = "AA:BB:CC:DD:EE:09" }],
                VpnSessions = snapshot.VpnSessions,
            });
        Assert.Contains(ipsec.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.VpnPeer && h.Detail == "ipsec");

        EndpointAttributionResult ppp = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery { Family = "ipv4", IpAddress = "10.10.10.2" },
            new EndpointAttributionSnapshot
            {
                ArpEntries = [new ArpFact { IpAddress = "10.10.10.2", MacAddress = "AA:BB:CC:DD:EE:10" }],
                VpnSessions = snapshot.VpnSessions,
            });
        Assert.Contains(ppp.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.VpnPeer && h.Value == "pppoe-client");
    }

    [Fact]
    public void VethInterfaceNameWithoutContainerProducesPartialFinding()
    {
        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "172.18.0.2",
            },
            new EndpointAttributionSnapshot
            {
                ArpEntries =
                [
                    new ArpFact
                    {
                        IpAddress = "172.18.0.2",
                        MacAddress = "02:42:AC:12:00:02",
                        Interface = "veth-orphan",
                    },
                ],
            });

        Assert.Equal(EndpointAttributionCertainty.Partial, result.Certainty);
        Assert.Contains(result.Findings, f => f.Code == EndpointAttributionCodes.VethPartial);
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Veth && h.Value == "veth-orphan");
    }

    [Fact]
    public async Task UseCaseMapsResultToView()
    {
        FakeAuthorizationBoundary auth = new();
        ResolveEndpointAttributionUseCase useCase = new(auth);
        ApplicationResult<EndpointAttributionView> result = await useCase.ExecuteAsync(
            new ResolveEndpointAttributionCommand
            {
                Actor = "tester",
                Query = new EndpointAttributionQuery
                {
                    Family = "ipv4",
                    IpAddress = "192.168.1.1",
                },
                Snapshot = new EndpointAttributionSnapshot
                {
                    DhcpLeases =
                    [
                        new DhcpLeaseFact
                        {
                            IpAddress = "192.168.1.1",
                            MacAddress = "AA:BB:CC:DD:EE:11",
                        },
                    ],
                },
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Partial", result.Value!.Certainty);
        Assert.Contains(result.Value.Hops, h => h.Kind == "Mac");
    }

    private static RosReadCommandResult Records(RosReadCommandId id, Dictionary<string, string> known)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records =
            [
                new RosReadRecord
                {
                    KnownProperties = known,
                    RawProperties = known,
                },
            ],
            SessionInvalidated = false,
            Error = null,
        };
}
