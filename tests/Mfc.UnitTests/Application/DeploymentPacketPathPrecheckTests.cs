using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class DeploymentPacketPathPrecheckTests
{
    [Fact]
    public void CanonicalHardwareOffloadBlocksDeployWithoutReclassification()
    {
        CanonicalRecord hw = Pair("ether1", "wan1", "HARDWARE_OFFLOADED_PATH");
        CanonicalRecord cpu = Pair("ether3", "wan2", "CPU_FIREWALL_PATH");
        Assert.Equal(
            PacketPathAnalysisCodes.BypassesIpFirewall,
            DeploymentPacketPathPrecheck.DescribeBlocker(NodeKind.Router, [hw, cpu]));
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentPacketPathPrecheck.EnsureCleared(NodeKind.Router, [hw]));
        Assert.StartsWith(PacketPathAnalysisCodes.BypassesIpFirewall, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalCpuPathAllowsDeployAndMissingClassIsNotProven()
    {
        DeploymentPacketPathPrecheck.EnsureCleared(
            NodeKind.Router,
            [Pair("ether1", "wan1", "CPU_FIREWALL_PATH")]);
        Assert.Equal(
            PacketPathAnalysisCodes.NotProven,
            DeploymentPacketPathPrecheck.DescribeBlocker(
                NodeKind.Router,
                [Pair("ether1", "wan1", className: null)]));
        Assert.Null(DeploymentPacketPathPrecheck.DescribeBlocker(NodeKind.Switch, []));
    }

    private static CanonicalRecord Pair(string ingress, string egress, string? className)
    {
        Dictionary<string, string> properties = new(StringComparer.Ordinal)
        {
            ["ingress"] = ingress,
            ["egress"] = egress,
        };
        if (className is not null)
        {
            properties["class"] = className;
        }

        return new CanonicalRecord(properties);
    }
}
