using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class TopologyDependencyContextMapperTests
{
    [Fact]
    public void CanonicalSectionsMapToDomainBlockersWithoutWritingNatOrVrrp()
    {
        CanonicalRecord vrrp = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["group"] = "Ipv4/vrid=1/if=ether1",
            ["interface"] = "ether1",
            ["vrid"] = "1",
            ["family"] = "Ipv4",
            ["sync-connection-tracking"] = "yes",
            ["connection-tracking-port"] = "8275",
            ["remote-address"] = "192.0.2.20",
            ["disabled"] = "false",
        });
        CanonicalRecord role = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["group"] = "Ipv4/vrid=1/if=ether1",
            ["role"] = "Master",
        });
        CanonicalRecord settings = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rp-filter"] = "strict",
        });
        CanonicalRecord raw = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "prerouting",
            ["action"] = "notrack",
        });
        CanonicalRecord mangle = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "prerouting",
            ["action"] = "mark-routing",
            ["new-routing-mark"] = "wan1",
            ["per-connection-classifier"] = "both-addresses:2/0",
        });
        CanonicalRecord table = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "wan2",
            ["fib"] = "yes",
        });

        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(
            NodeKind.Router,
            DeclaredUplinkMode.Failover,
            uplinks:
            [
                UplinkCoverageFact.Create("wan1", UplinkTrafficMode.Primary, "wan"),
                UplinkCoverageFact.Create("wan2", UplinkTrafficMode.Backup, zoneKey: null),
            ],
            declaredVrrpMemberIds: ["a", "b"],
            observedVrrpMemberIds: ["a"],
            observingDeviceId: "a",
            candidate: CandidatePolicySurface.Create(hasStatefulConnectionMatcher: true, hasDstNatMatcher: true));
        TopologyDependencyAnalysisResult result = TopologyDependencyContextMapper.Analyze(
            profile,
            new TopologyDependencyCanonicalSections
            {
                VrrpConfiguration = [vrrp],
                VrrpObservations = [role],
                RoutingTables = [table],
                Ipv4Settings = [settings],
                Ipv4Raw = [raw],
                Ipv4Mangle = [mangle],
            });

        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.VrrpMemberMissing);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.StrictRpfWithVrrp);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.RawNotrackIntersectsStateful);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.ManglePccPresent);
        Assert.Contains(result.ProtectedFlows, f => f.Kind == VrrpProtectedFlowKind.Sync);
        Assert.Equal(VrrpMemberObservedState.Master, Assert.Single(result.RoleVector).Role);
        Assert.False(result.HasCollapsedGlobalMaster);
    }

    [Fact]
    public void ActiveDefaultRouteObservationDoesNotEnterMappedContextHash()
    {
        CanonicalRecord vrrp = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["interface"] = "ether1",
            ["vrid"] = "1",
            ["family"] = "Ipv4",
        });
        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(observingDeviceId: "a");
        TopologyDependencyCanonicalSections reachable = new()
        {
            VrrpConfiguration = [vrrp],
            Ipv4DefaultState =
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gateway"] = "1.1.1.1",
                    ["active"] = "true",
                    ["gateway-status"] = "reachable",
                }),
            ],
        };
        TopologyDependencyCanonicalSections down = new()
        {
            VrrpConfiguration = [vrrp],
            Ipv4DefaultState =
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gateway"] = "9.9.9.9",
                    ["active"] = "false",
                    ["gateway-status"] = "unreachable",
                }),
            ],
        };
        TopologyDependencyAnalysisResult first = TopologyDependencyContextMapper.Analyze(profile, reachable);
        TopologyDependencyAnalysisResult second = TopologyDependencyContextMapper.Analyze(profile, down);
        Assert.Equal(first.TopologyDependencyContextHash.ToString(), second.TopologyDependencyContextHash.ToString());
        Assert.NotEqual(first.TopologyObservationHash.ToString(), second.TopologyObservationHash.ToString());
    }

    [Fact]
    public void SwitchKindWithoutChipEvidenceIsFailClosed()
    {
        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(NodeKind.Switch);
        TopologyDependencyAnalysisResult result = TopologyDependencyContextMapper.Analyze(
            profile,
            new TopologyDependencyCanonicalSections());
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchHardwareProfileUnknown);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchTransitPathNotProven);
    }
}
