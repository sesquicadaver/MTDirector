using Mfc.Domain.Policy;
using Mfc.RouterOs.Discovery;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class PacketPathBlockerMapperTests
{
    [Fact]
    public void ClassificationMapsToDomainBlockersWithoutDisablingOffload()
    {
        PacketPathClassificationResult classified = new()
        {
            Pairs =
            [
                Pair("ether1", "ether2", PacketPathClass.HardwareOffloadedPath, PacketPathBlockerHint.PacketPathBypassesIpFirewall),
                Pair("ether3", "ether4", PacketPathClass.Indeterminate, PacketPathBlockerHint.PacketPathNotProven),
                Pair("ether5", "ether6", PacketPathClass.MixedPath, PacketPathBlockerHint.None),
            ],
            WorstPathClass = PacketPathClass.Indeterminate,
            Findings = [],
            Warnings = [],
        };

        PacketPathAnalysisResult result = PacketPathBlockerMapper.Analyze(classified);
        Assert.Contains(result.Findings, f =>
            f.Code == PacketPathAnalysisCodes.BypassesIpFirewall && f.IngressInterface == "ether1");
        Assert.Contains(result.Findings, f =>
            f.Code == PacketPathAnalysisCodes.NotProven && f.IngressInterface == "ether3");
        Assert.DoesNotContain(result.Findings, f => f.IngressInterface == "ether5");
        Assert.True(result.BlocksManagedForwardPolicy);
        Assert.Equal(
            PacketPathKind.HardwareOffloadedPath,
            PacketPathBlockerMapper.FromClassification(classified)[0].PathClass);
    }

    private static PacketPathPairClassification Pair(
        string ingress,
        string egress,
        PacketPathClass pathClass,
        PacketPathBlockerHint hint)
        => new()
        {
            IngressInterface = ingress,
            EgressInterface = egress,
            Bridge = "bridge1",
            VlanId = null,
            PathClass = pathClass,
            BlockerHint = hint,
            Reasons = [],
        };
}
