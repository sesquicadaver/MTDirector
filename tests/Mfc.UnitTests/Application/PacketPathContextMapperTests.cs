using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class PacketPathContextMapperTests
{
    [Fact]
    public void CanonicalPairRecordsMapToDomainBlockersWithoutReclassification()
    {
        CanonicalRecord hw = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ingress"] = "ether1",
            ["egress"] = "ether2",
            ["bridge"] = "bridge1",
            ["class"] = "HARDWARE_OFFLOADED_PATH",
        });
        CanonicalRecord cpu = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ingress"] = "ether3",
            ["egress"] = "ether4",
            ["class"] = "CPU_FIREWALL_PATH",
        });

        PacketPathAnalysisResult result = PacketPathContextMapper.Analyze([hw, cpu]);
        Assert.Contains(result.Findings, f =>
            f.Code == PacketPathAnalysisCodes.BypassesIpFirewall && f.IngressInterface == "ether1");
        Assert.DoesNotContain(result.Findings, f => f.IngressInterface == "ether3");
        IReadOnlyList<PacketPathPairFact> mapped = PacketPathContextMapper.FromCanonicalPairs([hw]);
        Assert.Equal(PacketPathKind.HardwareOffloadedPath, Assert.Single(mapped).PathClass);
    }
}
