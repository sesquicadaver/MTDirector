using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PacketPathAnalysisTests
{
    [Fact]
    public void HardwareOffloadedPathEmitsBypassesBlocker()
    {
        PacketPathAnalysisResult result = PacketPathAnalysis.Analyze(
            [Pair("ether1", "ether2", PacketPathKind.HardwareOffloadedPath)]);
        Assert.True(result.BlocksManagedForwardPolicy);
        Assert.True(result.HasBlockers);
        PacketPathFinding finding = Assert.Single(result.Findings);
        Assert.Equal(PacketPathAnalysisCodes.BypassesIpFirewall, finding.Code);
        Assert.Equal(PacketPathAnalysisCodes.SeverityBlocker, finding.Severity);
        Assert.Equal("ether1", finding.IngressInterface);
        Assert.Equal("ether2", finding.EgressInterface);
    }

    [Fact]
    public void IndeterminatePathEmitsNotProvenBlocker()
    {
        PacketPathAnalysisResult result = PacketPathAnalysis.Analyze(
            [Pair("ether1", "ether99", PacketPathKind.Indeterminate)]);
        Assert.Contains(result.Findings, f =>
            f.Code == PacketPathAnalysisCodes.NotProven
            && f.Severity == PacketPathAnalysisCodes.SeverityBlocker);
    }

    [Fact]
    public void CpuFirewallPathDoesNotBlockManagedForward()
    {
        PacketPathAnalysisResult result = PacketPathAnalysis.Analyze(
            [Pair("ether1", "ether2", PacketPathKind.CpuFirewallPath)]);
        Assert.False(result.BlocksManagedForwardPolicy);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void MixedPathDoesNotEmitNext1ForwardBlockers()
    {
        PacketPathAnalysisResult result = PacketPathAnalysis.Analyze(
            [Pair("ether1", "ether2", PacketPathKind.MixedPath)]);
        Assert.False(result.BlocksManagedForwardPolicy);
        Assert.DoesNotContain(result.Findings, f => f.Code == PacketPathAnalysisCodes.BypassesIpFirewall);
        Assert.DoesNotContain(result.Findings, f => f.Code == PacketPathAnalysisCodes.NotProven);
    }

    [Fact]
    public void PacketPathHashEntersAnalysisContext()
    {
        PacketPathPairFact[] pairs = [Pair("ether1", "ether2", PacketPathKind.HardwareOffloadedPath)];
        PacketPathAnalysisResult first = PacketPathAnalysis.Analyze(pairs);
        PacketPathAnalysisResult second = PacketPathAnalysis.Analyze(pairs);
        Assert.Equal(first.PacketPathContextHash.ToString(), second.PacketPathContextHash.ToString());
        Assert.Equal(32, first.PacketPathContextHash.Bytes.Length);

        Hash256 actual = ActualFilterAnalysis.HashActualContext([]);
        Hash256 combined = PacketPathAnalysis.HashAnalysisContext(actual, first.PacketPathContextHash);
        Assert.Equal(combined.ToString(), PacketPathAnalysis.HashAnalysisContext(actual, first.PacketPathContextHash).ToString());
        Assert.NotEqual(ActualFilterAnalysis.HashAnalysisContext(actual).ToString(), combined.ToString());

        PacketPathAnalysisResult changed = PacketPathAnalysis.Analyze(
            [Pair("ether1", "ether2", PacketPathKind.CpuFirewallPath)]);
        Assert.NotEqual(first.PacketPathContextHash.ToString(), changed.PacketPathContextHash.ToString());
    }

    [Fact]
    public void FindingsAreIndependentOfInputOrder()
    {
        PacketPathPairFact hw = Pair("ether2", "ether1", PacketPathKind.HardwareOffloadedPath);
        PacketPathPairFact unknown = Pair("ether1", "ether3", PacketPathKind.Indeterminate);
        PacketPathAnalysisResult a = PacketPathAnalysis.Analyze([hw, unknown]);
        PacketPathAnalysisResult b = PacketPathAnalysis.Analyze([unknown, hw]);
        Assert.Equal(
            a.Findings.Select(f => (f.Code, f.IngressInterface, f.EgressInterface)),
            b.Findings.Select(f => (f.Code, f.IngressInterface, f.EgressInterface)));
        Assert.Equal(a.PacketPathContextHash.ToString(), b.PacketPathContextHash.ToString());
    }

    [Fact]
    public void PairAndClassInvariantsHold()
    {
        Assert.False(PacketPathAnalysisCodes.IsFailedPrecondition(string.Empty));
        Assert.True(PacketPathAnalysisCodes.IsFailedPrecondition(PacketPathAnalysisCodes.BypassesIpFirewall));
        Assert.Equal(PacketPathKind.CpuFirewallPath, PacketPathAnalysis.ParseClassName("CPU_FIREWALL_PATH"));
        Assert.Throws<DomainInvariantException>(() => PacketPathAnalysis.ParseClassName("not-a-class"));
        Assert.Throws<DomainInvariantException>(() =>
            PacketPathPairFact.Create("  ", "ether2", PacketPathKind.CpuFirewallPath));
        Assert.Throws<DomainInvariantException>(() =>
            PacketPathPairFact.Create("ether1", " ", PacketPathKind.CpuFirewallPath));
        Assert.Throws<DomainInvariantException>(() =>
            PacketPathPairFact.Create("ether1", "ether2", (PacketPathKind)99));
    }

    private static PacketPathPairFact Pair(string ingress, string egress, PacketPathKind kind)
        => PacketPathPairFact.Create(ingress, egress, kind, bridge: "bridge1");
}
