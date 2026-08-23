using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.3-03 AC (on-demand session context).</summary>
public sealed class IncidentSessionContextLivingSpecTests
{
    [Fact]
    public void Ac1ExactOriginalFlowResolvesSession()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            sourcePort: 52344,
            destinationAddress: "198.51.100.4",
            destinationPort: 443,
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(Entry(flow, connectionState: "established")));

        Assert.NotNull(result.Session);
        Assert.Equal("established", result.Session!.ConnectionState);
        Assert.Equal(SessionVisibilityStatus.Full, result.VisibilityStatus);
    }

    [Fact]
    public void Ac2MissingSessionReturnsNotObserved()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            protocol: "udp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            new ConnectionTrackingSnapshot());

        Assert.Null(result.Session);
        Assert.Equal(SessionVisibilityStatus.NotObserved, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == IncidentSessionContextCodes.SessionNotFound);
    }

    [Fact]
    public void Ac3AmbiguousMatchesFailClosed()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(Entry(flow), Entry(flow)));

        Assert.Null(result.Session);
        Assert.Contains(result.Findings, f => f.Code == IncidentSessionContextCodes.SessionAmbiguous);
    }

    [Fact]
    public void Ac4HwOffloadLimitsVisibilityToPartial()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(Entry(flow, hwOffload: true)));

        Assert.NotNull(result.Session);
        Assert.Equal(SessionVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == IncidentSessionContextCodes.HwOffloadLimitedVisibility);
    }

    [Fact]
    public void Ac5FastTrackLimitsVisibilityToPartial()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(Entry(flow, fastTrack: true)));

        Assert.NotNull(result.Session);
        Assert.Equal(SessionVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == IncidentSessionContextCodes.FastTrackLimitedVisibility);
    }

    [Fact]
    public void Ac6NatFlagsAreSurfaced()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(Entry(flow, srcNat: true, dstNat: true)));

        Assert.True(result.Session!.SrcNatActive);
        Assert.True(result.Session.DstNatActive);
    }

    [Fact]
    public void Ac7ReplyTupleMappedFromSnapshot()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            sourcePort: 52344,
            destinationAddress: "198.51.100.4",
            destinationPort: 443,
            protocol: "tcp");

        ConnectionTrackingEntryFact entry = new()
        {
            Protocol = "tcp",
            OriginalSourceAddress = "10.0.0.8",
            OriginalSourcePort = 52344,
            OriginalDestinationAddress = "198.51.100.4",
            OriginalDestinationPort = 443,
            ReplySourceAddress = "198.51.100.4",
            ReplySourcePort = 443,
            ReplyDestinationAddress = "10.0.0.8",
            ReplyDestinationPort = 52344,
        };

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            new ConnectionTrackingSnapshot { Entries = [entry] });

        Assert.NotNull(result.Session!.ReplyFlow);
        Assert.Equal("198.51.100.4", result.Session.ReplyFlow!.SourceAddress);
        Assert.Equal((ushort)443, result.Session.ReplyFlow.SourcePort);
    }

    [Fact]
    public void Ac8SnapshotMapperParsesRouterOsConnectionRows()
    {
        RosReadCommandResult read = new()
        {
            CommandId = RosReadCommandId.Ipv4FirewallConnections,
            Lifecycle = RosCommandLifecycle.Completed,
            Records =
            [
                new RosReadRecord
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["protocol"] = "tcp",
                        ["src-address"] = "10.0.0.8:52344",
                        ["dst-address"] = "198.51.100.4:443",
                        ["reply-src-address"] = "198.51.100.4:443",
                        ["reply-dst-address"] = "10.0.0.8:52344",
                        ["tcp-state"] = "established",
                        ["timeout"] = "23m45s",
                        ["fasttrack"] = "no",
                        ["hw-offload"] = "no",
                    },
                    RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
            SessionInvalidated = false,
            Error = null,
        };

        ConnectionTrackingSnapshot snapshot = ConnectionTrackingSnapshotMapper.Map(
            new Dictionary<RosReadCommandId, RosReadCommandResult>
            {
                [RosReadCommandId.Ipv4FirewallConnections] = read,
            });

        Assert.Single(snapshot.Entries);
        Assert.Equal("10.0.0.8", snapshot.Entries[0].OriginalSourceAddress);
        Assert.Equal((ushort)52344, snapshot.Entries[0].OriginalSourcePort);
    }

    [Fact]
    public void Ac9ConnectionTrackingAllowlistIsReadOnlyPrintPaths()
    {
        Assert.Equal(2, ConnectionTrackingAllowlist.FixedPaths.Count);
        Assert.All(ConnectionTrackingAllowlist.FixedPaths, path => Assert.EndsWith("/print", path, StringComparison.Ordinal));
        Assert.DoesNotContain(ConnectionTrackingAllowlist.FixedPaths, p => p.Contains("/add", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            protocol: "tcp");
        FakeAuthorizationBoundary auth = new();
        ResolveIncidentSessionContextUseCase useCase = new(auth);

        ApplicationResult<IncidentSessionContextResultView> ok = await useCase.ExecuteAsync(
            new ResolveIncidentSessionContextCommand
            {
                Actor = "analyst",
                Query = new IncidentSessionContextQuery { OriginalFlow = flow },
                Snapshot = Snapshot(Entry(flow)),
            });
        Assert.True(ok.IsSuccess);
        Assert.NotNull(ok.Value!.Session);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentSessionRead);
        ApplicationResult<IncidentSessionContextResultView> denied = await useCase.ExecuteAsync(
            new ResolveIncidentSessionContextCommand
            {
                Actor = "analyst",
                Query = new IncidentSessionContextQuery { OriginalFlow = flow },
                Snapshot = Snapshot(Entry(flow)),
            });
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error?.Code);
    }

    private static ConnectionTrackingSnapshot Snapshot(params ConnectionTrackingEntryFact[] entries)
        => new() { Entries = entries };

    private static ConnectionTrackingEntryFact Entry(
        FlowTuple flow,
        string? connectionState = null,
        bool srcNat = false,
        bool dstNat = false,
        bool fastTrack = false,
        bool hwOffload = false)
        => new()
        {
            Protocol = flow.Protocol!,
            OriginalSourceAddress = flow.SourceAddress!,
            OriginalSourcePort = flow.SourcePort,
            OriginalDestinationAddress = flow.DestinationAddress!,
            OriginalDestinationPort = flow.DestinationPort,
            ConnectionState = connectionState,
            SrcNatActive = srcNat,
            DstNatActive = dstNat,
            FastTrack = fastTrack,
            HwOffload = hwOffload,
        };
}
