using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class FastTrackContextMapperTests
{
    private static readonly ServiceObject Tcp = ServiceObject.Create(
        PolicyObjectOwnerScope.Company,
        null,
        null,
        NonEmptyName.Create("tcp"),
        [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);

    [Fact]
    public void CanonicalSingleWanMapsToSafeFastTrackWithoutCompilingFallback()
    {
        FastTrackAnalysisResult result = FastTrackContextMapper.Analyze(
            [AllowedRule()],
            TopologyDependencyProfile.Create(uplinkMode: DeclaredUplinkMode.One),
            new TopologyDependencyCanonicalSections(),
            catalog: new Dictionary<ServiceObjectId, ServiceObject> { [Tcp.Id] = Tcp });
        Assert.True(result.AllowsSafeFastTrack);
        Assert.True(result.RequiresAcceptFallback);
        Assert.Equal(FastTrackAnalysisCodes.RiskHigh, result.RiskFloor);
        Assert.DoesNotContain(result.Findings, f => f.Severity == FastTrackAnalysisCodes.SeverityBlocker);
    }

    [Fact]
    public void CanonicalPccAndPreAnchorAndVrfBlockFastTrack()
    {
        CanonicalRecord mangle = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "prerouting",
            ["action"] = "mark-routing",
            ["per-connection-classifier"] = "both-addresses:2/0",
            ["new-routing-mark"] = "wan1",
        });
        CanonicalRecord preAnchor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "forward",
            ["action"] = "fasttrack-connection",
            ["comment"] = "unmanaged",
        });
        CanonicalRecord anchor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "1",
            ["chain"] = "forward",
            ["action"] = "jump",
            ["comment"] = "fwc:anchor:forward",
        });
        CanonicalRecord vrf = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kind"] = "Vrf",
            ["name"] = "containers",
        });
        FastTrackAnalysisResult result = FastTrackContextMapper.Analyze(
            [AllowedRule()],
            TopologyDependencyProfile.Create(uplinkMode: DeclaredUplinkMode.One),
            new TopologyDependencyCanonicalSections { Ipv4Mangle = [mangle] },
            ipv4Filter: [preAnchor, anchor],
            packetPathNodes: [vrf],
            catalog: new Dictionary<ServiceObjectId, ServiceObject> { [Tcp.Id] = Tcp });
        Assert.Contains(result.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses);
        Assert.True(result.HasBlockers);
        Assert.False(result.AllowsSafeFastTrack);
    }

    private static PolicyRule AllowedRule()
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([Tcp.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [Tcp.Id] = Tcp }),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept));
}
