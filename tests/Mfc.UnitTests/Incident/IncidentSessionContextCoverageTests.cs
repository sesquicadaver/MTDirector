using Mfc.Application.Incident;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.RouterOs.Commands;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.3-03 incident session context modules.</summary>
public sealed class IncidentSessionContextCoverageTests
{
    [Fact]
    public void ResolverRejectsIncompleteOriginalFlow()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSessionContextResolver.Resolve(
                new IncidentSessionContextQuery
                {
                    OriginalFlow = FlowTuple.Create(sourceAddress: "10.0.0.1", protocol: "tcp"),
                },
                new ConnectionTrackingSnapshot()));
        Assert.Contains(IncidentSessionContextCodes.MissingOriginalFlow, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolverMatchesCaseInsensitiveProtocol()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.1",
            destinationAddress: "10.0.0.2",
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(new ConnectionTrackingEntryFact
            {
                Protocol = "TCP",
                OriginalSourceAddress = "10.0.0.1",
                OriginalDestinationAddress = "10.0.0.2",
            }));

        Assert.NotNull(result.Session);
    }

    [Fact]
    public void ResolverRejectsMismatchedPort()
    {
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.1",
            sourcePort: 2000,
            destinationAddress: "10.0.0.2",
            destinationPort: 443,
            protocol: "tcp");

        IncidentSessionContextResult result = IncidentSessionContextResolver.Resolve(
            new IncidentSessionContextQuery { OriginalFlow = flow },
            Snapshot(new ConnectionTrackingEntryFact
            {
                Protocol = "tcp",
                OriginalSourceAddress = "10.0.0.1",
                OriginalSourcePort = 1000,
                OriginalDestinationAddress = "10.0.0.2",
                OriginalDestinationPort = 443,
            }));

        Assert.Null(result.Session);
        Assert.Contains(result.Findings, f => f.Code == IncidentSessionContextCodes.SessionNotFound);
    }

    private static ConnectionTrackingSnapshot Snapshot(params ConnectionTrackingEntryFact[] entries)
        => new() { Entries = entries };

    [Fact]
    public void UseCaseValidatesNullSnapshot()
    {
        ResolveIncidentSessionContextUseCase useCase = new(new Mfc.UnitTests.Application.Fakes.FakeAuthorizationBoundary());
        Assert.Throws<ArgumentNullException>(() =>
            useCase.ExecuteAsync(
                new ResolveIncidentSessionContextCommand
                {
                    Actor = "tester",
                    Query = new IncidentSessionContextQuery
                    {
                        OriginalFlow = FlowTuple.Create(
                            sourceAddress: "10.0.0.1",
                            destinationAddress: "10.0.0.2",
                            protocol: "tcp"),
                    },
                    Snapshot = null!,
                }).GetAwaiter().GetResult());
    }

    [Fact]
    public void ConnectionTrackingRegistryIncludesNewCommands()
    {
        Assert.Equal("/ip/firewall/connection/print", RosReadCommandRegistry.Get(RosReadCommandId.Ipv4FirewallConnections).FixedPath);
        Assert.Equal("/ipv6/firewall/connection/print", RosReadCommandRegistry.Get(RosReadCommandId.Ipv6FirewallConnections).FixedPath);
    }
}
