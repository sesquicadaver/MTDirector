using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Extra branch coverage for M7.1-08 network path profile modules.</summary>
public sealed class NetworkPathProfileCoverageTests
{
    [Fact]
    public void BindRejectsMismatchedDestination()
    {
        NetworkPathProfile profile = new()
        {
            SourceDevice = DeviceId.New(),
            Destination = "203.0.113.10",
        };
        RouteResolutionTrace trace = new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.99",
        };

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() => NetworkPathProfileBinder.Bind(profile, trace));
        Assert.Contains("does not match", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AttachBindingsLeavesTracesWithoutProfilesUntouched()
    {
        RouteResolutionTrace trace = new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.10",
        };
        IReadOnlyList<RouteResolutionTrace> result = NetworkPathProfileBinder.AttachBindings([trace], []);
        Assert.Same(trace, Assert.Single(result));
    }

    [Fact]
    public void FingerprintDigestIsStable()
    {
        RoutePathFingerprint fingerprint = RoutePathFingerprint.FromTrace(new RouteResolutionTrace
        {
            Family = "ipv4",
            MatchedPrefix = "0.0.0.0/0",
            ImmediateNextHops = [new ImmediateNextHop { Gateway = "1.1.1.1", Interface = "ether1" }],
            EgressInterfaces = ["ether1"],
            ExecutionPath = RouteResolutionExecutionPaths.Cpu,
        });

        string first = fingerprint.ToDigest();
        string second = fingerprint.ToDigest();
        Assert.Equal(first, second);
        Assert.NotEqual(fingerprint, new RoutePathFingerprint { MatchedPrefix = "10.0.0.0/8" });
    }

    [Fact]
    public void EvaluateManyAggregatesFindings()
    {
        RouteResolutionTrace trace = new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.10",
            MatchedPrefix = "0.0.0.0/0",
            ImmediateNextHops = [new ImmediateNextHop { Gateway = "1.1.1.1" }],
            EgressInterfaces = ["ether1"],
            ExecutionPath = RouteResolutionExecutionPaths.Cpu,
        };
        NetworkPathProfile profile = new()
        {
            SourceDevice = DeviceId.New(),
            Destination = "203.0.113.10",
            MaxLoss = 1,
            MaxRtt = 10,
        };

        IReadOnlyList<RouteFinding> findings = NetworkPathLatencyEvaluator.EvaluateMany(
        [
            new NetworkPathLatencyEvaluationInput
            {
                Profile = profile,
                Trace = trace,
                Measurement = new LatencyMeasurement
                {
                    PacketLossPercent = 5,
                    RoundTripTimeMs = 50,
                    JitterMs = 1,
                },
            },
        ]);

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, static f => f.Code == NetworkPathProfileCodes.LatencyLossHigh);
        Assert.Contains(findings, static f => f.Code == NetworkPathProfileCodes.LatencyRttHigh);
    }
}
