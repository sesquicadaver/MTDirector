using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Topology;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Topology;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Topology;

public sealed class NodeTopologyValidatorTests
{
    [Fact]
    public void StandaloneRouterWithSingleDefaultRouteIsValid()
    {
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.One);
        DeviceTopologyFacts facts = BoundFacts(
            device,
            ObservedUplinkEvidence.SingleDefaultRoute,
            uplinkInterfaceCount: 1);

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(node, [facts]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Findings);
        Assert.Equal(ObservedUplinkEvidence.SingleDefaultRoute, result.EffectiveUplinkEvidence);
        Assert.False(result.UsedCapabilityCache);
    }

    [Fact]
    public void FailoverModeRequiresDistanceRouteEvidenceNotInterfaceCount()
    {
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.Failover);

        NodeTopologyValidationResult countOnly = NodeTopologyValidator.Validate(
            node,
            [
                BoundFacts(
                    device,
                    ObservedUplinkEvidence.Insufficient,
                    uplinkInterfaceCount: 3),
            ]);

        Assert.Contains(
            countOnly.Findings,
            static f => f.Code == TopologyValidationFinding.UplinkModeUncertain);
        Assert.Equal(ObservedUplinkEvidence.Insufficient, countOnly.EffectiveUplinkEvidence);

        NodeTopologyValidationResult withEvidence = NodeTopologyValidator.Validate(
            node,
            [
                BoundFacts(
                    device,
                    ObservedUplinkEvidence.FailoverDistanceRoutes,
                    uplinkInterfaceCount: 3),
            ]);

        Assert.True(withEvidence.IsValid);
        Assert.Equal(ObservedUplinkEvidence.FailoverDistanceRoutes, withEvidence.EffectiveUplinkEvidence);
    }

    [Fact]
    public void PccBalancedModeRequiresPccOrEcmpEvidence()
    {
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.Balanced);

        NodeTopologyValidationResult rejected = NodeTopologyValidator.Validate(
            node,
            [
                BoundFacts(
                    device,
                    ObservedUplinkEvidence.Insufficient,
                    uplinkInterfaceCount: 2),
            ]);
        Assert.Contains(
            rejected.Findings,
            static f => f.Code == TopologyValidationFinding.UplinkModeUncertain);

        NodeTopologyValidationResult accepted = NodeTopologyValidator.Validate(
            node,
            [
                BoundFacts(
                    device,
                    ObservedUplinkEvidence.BalancedPccOrEcmp,
                    uplinkInterfaceCount: 2),
            ]);
        Assert.True(accepted.IsValid);
    }

    [Fact]
    public void RouterWithTwoAttachedDevicesIsRejected()
    {
        Node node = Node.Reconstitute(
            NodeId.New(),
            SiteId.New(),
            NonEmptyName.Create("bad-router"),
            NodeKind.Router,
            DeclaredUplinkMode.One,
            NodeStatus.Draft,
            rowVersion: 1);

        Device a = Device.Reconstitute(
            DeviceId.New(),
            node.Id,
            NonEmptyName.Create("a"),
            ManagementEndpoint.Create("10.0.0.1"),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            rowVersion: 1);
        Device b = Device.Reconstitute(
            DeviceId.New(),
            node.Id,
            NonEmptyName.Create("b"),
            ManagementEndpoint.Create("10.0.0.2"),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            rowVersion: 1);
        node.AttachDevice(a);
        node.AttachDevice(b);

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(
            node,
            [
                BoundFacts(a, ObservedUplinkEvidence.SingleDefaultRoute, 1),
                BoundFacts(b, ObservedUplinkEvidence.SingleDefaultRoute, 1),
            ]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.RouterCardinalityViolation
                && f.Severity == TopologyFindingSeverity.Blocker);
    }

    [Fact]
    public void VrrpWithOneDeviceIsRejected()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("vrrp-one"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.Failover);
        Device only = node.AddDevice(
            NonEmptyName.Create("m1"),
            ManagementEndpoint.Create("10.0.1.1"),
            DeviceRole.Router);

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(
            node,
            [BoundFacts(only, ObservedUplinkEvidence.FailoverDistanceRoutes, 2)]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.VrrpCardinalityViolation);
    }

    [Fact]
    public void SplitMasterVrrpCreatesBlocker()
    {
        (Node node, Device m1, Device m2) = CreateVrrpPair();

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(
            node,
            [
                VrrpFacts(m1, "7.16.1", VrrpMemberObservedState.Master),
                VrrpFacts(m2, "7.16.1", VrrpMemberObservedState.Master),
            ]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.VrrpSplitMaster
                && f.Severity == TopologyFindingSeverity.Blocker);
    }

    [Fact]
    public void VersionMismatchOnSameVridCreatesBlocker()
    {
        (Node node, Device m1, Device m2) = CreateVrrpPair();

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(
            node,
            [
                VrrpFacts(m1, "7.16.1", VrrpMemberObservedState.Master),
                VrrpFacts(m2, "7.15.3", VrrpMemberObservedState.Backup),
            ]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.VrrpVersionMismatch);
    }

    [Fact]
    public void VrrpGroupsMustBePresentOnAllMembers()
    {
        (Node node, Device m1, Device m2) = CreateVrrpPair();

        DeviceTopologyFacts facts1 = VrrpFacts(m1, "7.16.1", VrrpMemberObservedState.Master);
        DeviceTopologyFacts facts2 = BoundFacts(m2, ObservedUplinkEvidence.FailoverDistanceRoutes, 2) with
        {
            VrrpInstances = [],
            RouterOsVersion = "7.16.1",
        };

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(node, [facts1, facts2]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.VrrpGroupMembershipMismatch);
    }

    [Fact]
    public void SwitchMustNotGrantTransitFirewallCapability()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("sw"),
            NodeKind.Switch,
            DeclaredUplinkMode.None);
        Device device = node.AddDevice(
            NonEmptyName.Create("sw-dev"),
            ManagementEndpoint.Create("10.0.2.1"),
            DeviceRole.L2Switch);

        DeviceTopologyFacts facts = BoundFacts(device, ObservedUplinkEvidence.None, 0) with
        {
            BoardRole = ObservedBoardRole.Switch,
            GrantsTransitFirewallCapability = true,
        };

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(node, [facts]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.SwitchTransitFirewallForbidden);
    }

    [Fact]
    public void UnboundDeviceFactsProduceBlocker()
    {
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.One);
        DeviceTopologyFacts facts = BoundFacts(device, ObservedUplinkEvidence.SingleDefaultRoute, 1) with
        {
            IsExplicitlyBoundToNode = false,
        };

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(node, [facts]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.DeviceNotBoundToNode);
    }

    [Fact]
    public void UncertainBoardRoleSurfacesFindingNotAssumption()
    {
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.One);
        DeviceTopologyFacts facts = BoundFacts(device, ObservedUplinkEvidence.SingleDefaultRoute, 1) with
        {
            BoardRole = ObservedBoardRole.Unknown,
        };

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(node, [facts]);

        Assert.True(result.IsValid);
        Assert.Contains(
            result.Findings,
            static f => f.Code == TopologyValidationFinding.BoardRoleUncertain
                && f.Severity == TopologyFindingSeverity.Finding);
    }

    [Fact]
    public void ValidationResultIsDeterministic()
    {
        (Node node, Device m1, Device m2) = CreateVrrpPair();
        IReadOnlyList<DeviceTopologyFacts> facts =
        [
            VrrpFacts(m1, "7.16.1", VrrpMemberObservedState.Master),
            VrrpFacts(m2, "7.15.3", VrrpMemberObservedState.Master),
        ];

        NodeTopologyValidationResult first = NodeTopologyValidator.Validate(node, facts);
        NodeTopologyValidationResult second = NodeTopologyValidator.Validate(node, facts);

        Assert.Equal(NodeTopologyValidator.Fingerprint(first), NodeTopologyValidator.Fingerprint(second));
        Assert.Equal(first.Findings.Select(static f => f.Code), second.Findings.Select(static f => f.Code));
    }

    [Fact]
    public void CapabilityCacheShortCircuitsWhenStillValid()
    {
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.One);
        CapabilityHash hash = CapabilityHash.ParseHex(new string('a', 64));
        TopologyValidationCache cache = new();
        cache.RememberValidated(hash);

        DeviceTopologyFacts facts = BoundFacts(device, ObservedUplinkEvidence.SingleDefaultRoute, 1) with
        {
            CapabilityHash = hash,
        };

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(
            node,
            [facts],
            new Dictionary<DeviceId, TopologyValidationCache> { [device.Id] = cache });

        Assert.True(result.IsValid);
        Assert.True(result.UsedCapabilityCache);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task UseCaseRequiresInventoryReadPermission()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        ValidateNodeTopologyUseCase useCase = new(auth);
        (Node node, Device device) = CreateRouterNode(DeclaredUplinkMode.One);

        ApplicationResult<NodeTopologyValidationResult> denied = await useCase.ExecuteAsync(
            new ValidateNodeTopologyCommand
            {
                Actor = "tester",
                Node = node,
                DeviceFacts = [BoundFacts(device, ObservedUplinkEvidence.SingleDefaultRoute, 1)],
            });

        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error!.Code);

        auth.DeniedPermissions.Clear();
        ApplicationResult<NodeTopologyValidationResult> ok = await useCase.ExecuteAsync(
            new ValidateNodeTopologyCommand
            {
                Actor = "tester",
                Node = node,
                DeviceFacts = [BoundFacts(device, ObservedUplinkEvidence.SingleDefaultRoute, 1)],
            });

        Assert.True(ok.IsSuccess);
        Assert.True(ok.Value!.IsValid);
    }

    private static (Node Node, Device Device) CreateRouterNode(DeclaredUplinkMode mode)
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("r1"),
            NodeKind.Router,
            mode);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1-dev"),
            ManagementEndpoint.Create("10.0.0.1"),
            DeviceRole.Router);
        return (node, device);
    }

    private static (Node Node, Device M1, Device M2) CreateVrrpPair()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("vrrp"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.Failover);
        Device m1 = node.AddDevice(
            NonEmptyName.Create("m1"),
            ManagementEndpoint.Create("10.0.1.1"),
            DeviceRole.Router);
        Device m2 = node.AddDevice(
            NonEmptyName.Create("m2"),
            ManagementEndpoint.Create("10.0.1.2"),
            DeviceRole.Router);
        return (node, m1, m2);
    }

    private static DeviceTopologyFacts BoundFacts(
        Device device,
        ObservedUplinkEvidence evidence,
        int uplinkInterfaceCount)
        => new()
        {
            DeviceId = device.Id,
            RouterOsVersion = "7.16.1",
            BoardRole = ObservedBoardRole.Router,
            IsExplicitlyBoundToNode = true,
            VrrpInstances = [],
            UplinkEvidence = evidence,
            ObservedUplinkInterfaceCount = uplinkInterfaceCount,
            GrantsTransitFirewallCapability = true,
            CapabilityHash = null,
        };

    private static DeviceTopologyFacts VrrpFacts(
        Device device,
        string version,
        VrrpMemberObservedState state)
        => BoundFacts(device, ObservedUplinkEvidence.FailoverDistanceRoutes, 2) with
        {
            RouterOsVersion = version,
            VrrpInstances =
            [
                new ObservedVrrpInstance
                {
                    Family = IpAddressFamily.IPv4,
                    Vrid = 10,
                    InterfaceKey = "ether1",
                    ObservedState = state,
                    RouterOsVersion = version,
                },
            ],
        };
}
