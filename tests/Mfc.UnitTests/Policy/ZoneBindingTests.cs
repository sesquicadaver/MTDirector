using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ZoneDefinitionTests
{
    [Fact]
    public void CompanyZoneRejectsOwnerId()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ZoneDefinition.Create(
                PolicyOwnerScope.Company,
                Guid.NewGuid(),
                NonEmptyName.Create("mgmt"),
                NonEmptyName.Create("Management")));
    }

    [Fact]
    public void SiteZoneRequiresOwnerIdAndSupportsRenameConcurrency()
    {
        Guid siteId = Guid.NewGuid();
        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Site,
            siteId,
            NonEmptyName.Create("lan"),
            NonEmptyName.Create("LAN"));
        Assert.Equal(1UL, zone.RowVersion);
        zone.Rename(NonEmptyName.Create("LAN Core"));
        Assert.Equal(2UL, zone.RowVersion);
        Assert.Equal("LAN Core", zone.Name.Value);
    }
}

public sealed class NodeZoneBindingTests
{
    [Fact]
    public void SingleInterfaceRequiresExactlyOneValue()
    {
        Assert.Throws<DomainInvariantException>(() =>
            NodeZoneBinding.Create(
                new NodeId(Guid.NewGuid()),
                ZoneId.New(),
                NodeZoneBindingKind.SingleInterface,
                ["ether1", "ether2"],
                Hash256.Create(new byte[32])));
    }

    [Fact]
    public void RecordResolveMarksAnalysisStaleOnHashMismatch()
    {
        Hash256 expected = Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray());
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            expected);
        Assert.True(binding.AnalysisStale);

        Hash256 fresh = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            ["ether1"]);
        binding.RecordResolve(fresh);
        Assert.True(binding.AnalysisStale);
        Assert.Equal(fresh, binding.LastResolvedDependencyHash);

        binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            fresh);
        binding.RecordResolve(fresh);
        Assert.False(binding.AnalysisStale);
    }
}

public sealed class ZoneResolveEngineTests
{
    [Fact]
    public void MissingDynamicAndEmptyProduceBlockersPerDevice()
    {
        ZoneId zoneId = ZoneId.New();
        NodeId nodeId = new(Guid.NewGuid());
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            ["ether1", "ether2"]);
        NodeZoneBinding binding = NodeZoneBinding.Create(
            nodeId,
            zoneId,
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            expected);

        DeviceId deviceA = new(Guid.NewGuid());
        DeviceId deviceB = new(Guid.NewGuid());

        ZoneBindingResolveResult a = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = deviceA,
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                    new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = true },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });

        Assert.Contains(a.Blockers, b => b.Code == ZoneResolveBlockerCodes.DynamicInterface);
        Assert.Equal(["ether1"], a.ResolvedMembers);
        Assert.True(a.AnalysisStale);

        ZoneBindingResolveResult b = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = deviceB,
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "sfp1", Dynamic = false },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });

        Assert.Contains(b.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingInterface);
        Assert.Contains(b.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
        Assert.Empty(b.ResolvedMembers);
    }

    [Fact]
    public void InterfaceListIncludeExcludeHonoredAndVrrpNamesMayDiffer()
    {
        ZoneId zoneId = ZoneId.New();
        Hash256 expected = Hash256.Create(new byte[32]);
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            zoneId,
            NodeZoneBindingKind.InterfaceList,
            ["LAN"],
            expected);

        ZoneResolveDeviceObservation member1 = new()
        {
            DeviceId = new DeviceId(Guid.NewGuid()),
            ObservationAvailable = true,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "ether3", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "ether4", Dynamic = false },
            ],
            InterfaceLists =
            [
                new InterfaceListSpec { Name = "LAN", Include = ["ACCESS"], Exclude = ["GUEST"] },
                new InterfaceListSpec { Name = "ACCESS", Include = [], Exclude = [] },
                new InterfaceListSpec { Name = "GUEST", Include = [], Exclude = [] },
            ],
            InterfaceListMembers =
            [
                new InterfaceListMemberSpec { List = "ACCESS", Interface = "ether2", Disabled = false },
                new InterfaceListMemberSpec { List = "ACCESS", Interface = "ether3", Disabled = false },
                new InterfaceListMemberSpec { List = "GUEST", Interface = "ether3", Disabled = false },
                new InterfaceListMemberSpec { List = "LAN", Interface = "ether4", Disabled = false },
            ],
        };

        ZoneResolveDeviceObservation member2 = new()
        {
            DeviceId = new DeviceId(Guid.NewGuid()),
            ObservationAvailable = true,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "sfp-sfpplus1", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "sfp-sfpplus2", Dynamic = false },
            ],
            InterfaceLists =
            [
                new InterfaceListSpec { Name = "LAN", Include = [], Exclude = [] },
            ],
            InterfaceListMembers =
            [
                new InterfaceListMemberSpec { List = "LAN", Interface = "sfp-sfpplus1", Disabled = false },
                new InterfaceListMemberSpec { List = "LAN", Interface = "sfp-sfpplus2", Disabled = false },
            ],
        };

        ZoneBindingResolveResult r1 = ZoneResolveEngine.Resolve(binding, member1);
        ZoneBindingResolveResult r2 = ZoneResolveEngine.Resolve(binding, member2);

        Assert.Equal(["ether2", "ether4"], r1.ResolvedMembers);
        Assert.Equal(["sfp-sfpplus1", "sfp-sfpplus2"], r2.ResolvedMembers);
        Assert.DoesNotContain(r1.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
        Assert.DoesNotContain(r2.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }

    [Fact]
    public void ObservationUnavailableProducesTypedBlocker()
    {
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult result = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = false,
                Interfaces = [],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });

        Assert.Contains(result.Blockers, b => b.Code == ZoneResolveBlockerCodes.ObservationUnavailable);
        Assert.True(result.AnalysisStale);
    }

    [Fact]
    public void InterfaceListCycleAndMissingListProduceTypedBlockers()
    {
        NodeZoneBinding binding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.InterfaceList,
            ["LAN"],
            Hash256.Create(new byte[32]));

        ZoneBindingResolveResult cycle = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                ],
                InterfaceLists =
                [
                    new InterfaceListSpec { Name = "LAN", Include = ["WAN"], Exclude = [] },
                    new InterfaceListSpec { Name = "WAN", Include = ["LAN"], Exclude = [] },
                ],
                InterfaceListMembers =
                [
                    new InterfaceListMemberSpec { List = "LAN", Interface = "ether1", Disabled = false },
                ],
            });
        Assert.Contains(cycle.Blockers, b => b.Code == ZoneResolveBlockerCodes.InterfaceListCycle);

        ZoneBindingResolveResult missing = ZoneResolveEngine.Resolve(
            binding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
            });
        Assert.Contains(missing.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingInterfaceList);
        Assert.Contains(missing.Blockers, b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet);
    }
}

public sealed class ZoneDefinitionOwnerScopeTests
{
    [Fact]
    public void NodeScopeAndReconstituteValidateOwners()
    {
        Guid nodeId = Guid.NewGuid();
        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Node,
            nodeId,
            NonEmptyName.Create("guest"),
            NonEmptyName.Create("Guest"),
            "  note  ");
        Assert.Equal("note", zone.Description);
        zone.SetDescription(null);
        Assert.Null(zone.Description);

        ZoneDefinition reconstituted = ZoneDefinition.Reconstitute(
            zone.Id,
            zone.OwnerScope,
            zone.OwnerId,
            zone.Key,
            zone.Name,
            "again",
            zone.RowVersion);
        Assert.Equal("again", reconstituted.Description);

        Assert.Throws<DomainInvariantException>(() =>
            ZoneDefinition.Create(
                PolicyOwnerScope.Node,
                null,
                NonEmptyName.Create("x"),
                NonEmptyName.Create("X")));
        Assert.Throws<DomainInvariantException>(() =>
            ZoneDefinition.Reconstitute(
                ZoneId.New(),
                PolicyOwnerScope.Company,
                null,
                NonEmptyName.Create("x"),
                NonEmptyName.Create("X"),
                null,
                rowVersion: 0));
    }
}
