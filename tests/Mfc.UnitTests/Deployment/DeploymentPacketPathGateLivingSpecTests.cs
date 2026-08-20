using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for N1-06: block deploy when packet-path blockers are present
/// (next-1 + Safe Deployment PRECHECKING → BLOCKED).
/// </summary>
public sealed class DeploymentPacketPathGateLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1HardwareOffloadBlocksDeployWithBypassesCode()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node, plan, [], T0, DeploymentTestFactory.HardwareOffloadedPairs()));
        Assert.StartsWith(PacketPathAnalysisCodes.BypassesIpFirewall, ex.Message, StringComparison.Ordinal);
        Assert.Equal(
            PacketPathAnalysisCodes.BypassesIpFirewall,
            DeploymentPacketPathGate.DescribeBlocker(NodeKind.Router, DeploymentTestFactory.HardwareOffloadedPairs()));
    }

    [Fact]
    public void Ac2IndeterminateBlocksDeployWithNotProvenCode()
    {
        Assert.Equal(
            PacketPathAnalysisCodes.NotProven,
            DeploymentPacketPathGate.DescribeBlocker(NodeKind.Router, DeploymentTestFactory.IndeterminatePairs()));
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node, plan, [], T0, DeploymentTestFactory.IndeterminatePairs()));
        Assert.StartsWith(PacketPathAnalysisCodes.NotProven, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3CpuFirewallPathAllowsDeployStart()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperationGate.EnsureCanStart(node, plan, [], T0, DeploymentTestFactory.CpuPairs());
        Assert.Null(DeploymentPacketPathGate.DescribeBlocker(NodeKind.Router, DeploymentTestFactory.CpuPairs()));
    }

    [Fact]
    public void Ac4MixedPathDoesNotBlockDeploy()
    {
        Assert.Null(DeploymentPacketPathGate.DescribeBlocker(NodeKind.Router, DeploymentTestFactory.MixedPairs()));
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperationGate.EnsureCanStart(node, plan, [], T0, DeploymentTestFactory.MixedPairs());
    }

    [Fact]
    public void Ac5EmptyPairsOnRouterAreNotProven()
    {
        Assert.Equal(
            PacketPathAnalysisCodes.NotProven,
            DeploymentPacketPathGate.DescribeBlocker(NodeKind.Router, []));
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentPacketPathGate.EnsureCleared(NodeKind.Router, []));
    }

    [Fact]
    public void Ac6SwitchDoesNotRequireForwardPacketPathProof()
    {
        Assert.Null(DeploymentPacketPathGate.DescribeBlocker(NodeKind.Switch, []));
        Assert.Null(
            DeploymentPacketPathGate.DescribeBlocker(NodeKind.Switch, DeploymentTestFactory.HardwareOffloadedPairs()));
        Node node = DeploymentTestFactory.SwitchWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperationGate.EnsureCanStart(node, plan, [], T0, []);
    }

    [Fact]
    public void Ac7VrrpHardwareOffloadBlocksTheWholeNode()
    {
        Node node = DeploymentTestFactory.VrrpWithMembers(out _, out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node, plan, [], T0, DeploymentTestFactory.HardwareOffloadedPairs()));
        Assert.StartsWith(PacketPathAnalysisCodes.BypassesIpFirewall, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac8PacketPathBlockersFinishPrecheckAsBlockedWithoutStaging()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        Assert.False(
            DeploymentPacketPathGate.TryAllowStaging(
                operation, node.DeclaredKind, DeploymentTestFactory.HardwareOffloadedPairs(), T0.AddSeconds(1)));
        Assert.Equal(DeploymentOperationState.Blocked, operation.State);
        Assert.Equal(PacketPathAnalysisCodes.BypassesIpFirewall, operation.ErrorCode);
        Assert.True(operation.IsTerminal);
        Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(DeploymentOperationState.Staging, T0.AddSeconds(2)));
    }

    [Fact]
    public void Ac9ProvenPathAllowsPrecheckWithoutEnteringStaging()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        Assert.True(
            DeploymentPacketPathGate.TryAllowStaging(
                operation, node.DeclaredKind, DeploymentTestFactory.CpuPairs(), T0.AddSeconds(1)));
        Assert.Equal(DeploymentOperationState.Created, operation.State);
    }

    [Fact]
    public void Ac10GateDoesNotReferenceRouterOsOrOffloadWrites()
    {
        Assert.DoesNotContain(
            typeof(DeploymentPacketPathGate).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs", StringComparison.Ordinal));
        string sourceHint = typeof(DeploymentPacketPathGate).FullName ?? string.Empty;
        Assert.DoesNotContain("l3hw", sourceHint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("DEPLOYMENT_PACKET_PATH_BLOCKED", DeploymentCodes.PacketPathBlocked);
        Assert.True(PacketPathAnalysisCodes.IsFailedPrecondition(PacketPathAnalysisCodes.BypassesIpFirewall));
        Assert.True(PacketPathAnalysisCodes.IsFailedPrecondition(PacketPathAnalysisCodes.NotProven));
    }
}
