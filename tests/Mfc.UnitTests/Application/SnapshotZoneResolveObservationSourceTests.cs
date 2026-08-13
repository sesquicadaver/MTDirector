using Mfc.Application.Zones;
using Mfc.Domain;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class SnapshotZoneResolveObservationSourceTests
{
    [Fact]
    public async Task MissingCaptureOrInterfaceSectionYieldsUnavailable()
    {
        FakeDeviceStore devices = new();
        FakeSnapshotStore snapshots = new();
        SnapshotZoneResolveObservationSource source = new(devices, snapshots);

        DeviceId orphan = DeviceId.New();
        ZoneResolveDeviceObservation missingDevice = await source.GetForDeviceAsync(orphan);
        Assert.False(missingDevice.ObservationAvailable);

        Site site = Site.Create(SiteCode.Create("OBS"), NonEmptyName.Create("Obs"));
        Node node = Node.Create(site.Id, NonEmptyName.Create("n1"), NodeKind.Router, DeclaredUplinkMode.One);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("203.0.113.10"),
            DeviceRole.Router);
        await devices.AddAsync(device);

        ZoneResolveDeviceObservation noCapture = await source.GetForDeviceAsync(device.Id);
        Assert.False(noCapture.ObservationAvailable);

        Guid captureId = Guid.NewGuid();
        device.RecordCompletedCapture(captureId);
        await devices.UpdateAsync(device);
        snapshots.SectionsBySnapshot[captureId] = [];
        ZoneResolveDeviceObservation emptySections = await source.GetForDeviceAsync(device.Id);
        Assert.False(emptySections.ObservationAvailable);

        snapshots.SectionsBySnapshot[captureId] =
        [
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.SystemIdentity,
                ordered: false,
                [new CanonicalRecord(new Dictionary<string, string> { ["identity"] = "x" })]),
        ];
        ZoneResolveDeviceObservation noInterfaces = await source.GetForDeviceAsync(device.Id);
        Assert.False(noInterfaces.ObservationAvailable);
    }

    [Fact]
    public async Task ParsesInterfacesAndInterfaceListsFromCanonicalSections()
    {
        FakeDeviceStore devices = new();
        FakeSnapshotStore snapshots = new();
        SnapshotZoneResolveObservationSource source = new(devices, snapshots);

        Site site = Site.Create(SiteCode.Create("OBS2"), NonEmptyName.Create("Obs2"));
        Node node = Node.Create(site.Id, NonEmptyName.Create("n1"), NodeKind.Router, DeclaredUplinkMode.One);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("203.0.113.11"),
            DeviceRole.Router);
        Guid captureId = Guid.NewGuid();
        device.RecordCompletedCapture(captureId);
        await devices.AddAsync(device);

        snapshots.SectionsBySnapshot[captureId] =
        [
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.NetworkInterfaces,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "ether1",
                        ["dynamic"] = "false",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "ether2",
                        ["dynamic"] = "yes",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "ether1",
                        ["dynamic"] = "1",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = " ",
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.NetworkInterfaces,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "ether3",
                        ["dynamic"] = "no",
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.NetworkInterfaceLists,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["list"] = "LAN",
                        ["members"] = "ether1, ether2",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["list"] = " ",
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.NetworkInterfaceLists,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["list"] = "IGNORED",
                        ["members"] = "ether9",
                    }),
                ]),
        ];

        ZoneResolveDeviceObservation observation = await source.GetForDeviceAsync(device.Id);
        Assert.True(observation.ObservationAvailable);
        Assert.Equal(3, observation.Interfaces.Count);
        Assert.Contains(observation.Interfaces, i => i.Name == "ether1" && i.Dynamic);
        Assert.Contains(observation.Interfaces, i => i.Name == "ether2" && i.Dynamic);
        Assert.Contains(observation.Interfaces, i => i.Name == "ether3" && !i.Dynamic);
        Assert.Contains(observation.InterfaceLists, l => l.Name == "LAN");
        Assert.DoesNotContain(observation.InterfaceLists, l => l.Name == "IGNORED");
        Assert.Equal(2, observation.InterfaceListMembers.Count(m => m.List == "LAN"));
    }

    [Fact]
    public async Task ParsesContainerVethAndSharedVethCanonicalSections()
    {
        // AC-H
        FakeDeviceStore devices = new();
        FakeSnapshotStore snapshots = new();
        SnapshotZoneResolveObservationSource source = new(devices, snapshots);

        Site site = Site.Create(SiteCode.Create("OBS3"), NonEmptyName.Create("Obs3"));
        Node node = Node.Create(site.Id, NonEmptyName.Create("n1"), NodeKind.Router, DeclaredUplinkMode.One);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("203.0.113.12"),
            DeviceRole.Router);
        Guid captureId = Guid.NewGuid();
        device.RecordCompletedCapture(captureId);
        await devices.AddAsync(device);

        snapshots.SectionsBySnapshot[captureId] =
        [
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.NetworkInterfaces,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "veth1",
                        ["dynamic"] = "false",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "veth2",
                        ["dynamic"] = "false",
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.TopologyContainerVeth,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["endpoint_kind"] = "container",
                        ["endpoint_name"] = "pihole",
                        ["veth_name"] = "veth1",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["endpoint_kind"] = "app",
                        ["endpoint_name"] = "store",
                        ["veth_name"] = "veth2",
                    }),
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["endpoint_kind"] = "other",
                        ["endpoint_name"] = "x",
                        ["veth_name"] = "veth9",
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.TopologySharedVeth,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string> { ["veth_name"] = "veth1" }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.TopologyContainerVeth,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["endpoint_kind"] = "container",
                        ["endpoint_name"] = "ignored-obs",
                        ["veth_name"] = "veth1",
                    }),
                ]),
        ];

        ZoneResolveDeviceObservation observation = await source.GetForDeviceAsync(device.Id);
        Assert.True(observation.ObservationAvailable);
        Assert.Equal(2, observation.ContainerVethEdges.Count);
        Assert.Contains(
            observation.ContainerVethEdges,
            e => e.EndpointKind == "container" && e.EndpointName == "pihole" && e.VethName == "veth1");
        Assert.Contains(
            observation.ContainerVethEdges,
            e => e.EndpointKind == "app" && e.EndpointName == "store" && e.VethName == "veth2");
        Assert.Equal(["veth1"], observation.SharedVethNames);

        ZoneBindingResolveResult expanded = ZoneResolveEngine.Resolve(
            NodeZoneBinding.Create(
                node.Id,
                ZoneId.New(),
                NodeZoneBindingKind.SingleInterface,
                ["container:pihole"],
                Hash256.Create(new byte[32])),
            observation);
        Assert.Equal(["veth1"], expanded.ResolvedMembers);
        Assert.Contains(expanded.Blockers, b => b.Code == ZoneResolveBlockerCodes.SharedVeth);
    }

    [Fact]
    public async Task InterfacesWithoutTopologySectionsYieldTypedMarkerBlockers()
    {
        // AC-H2: interfaces present, topology sections absent → typed missing (not MISSING_INTERFACE on marker).
        FakeDeviceStore devices = new();
        FakeSnapshotStore snapshots = new();
        SnapshotZoneResolveObservationSource source = new(devices, snapshots);

        Site site = Site.Create(SiteCode.Create("OBS4"), NonEmptyName.Create("Obs4"));
        Node node = Node.Create(site.Id, NonEmptyName.Create("n1"), NodeKind.Router, DeclaredUplinkMode.One);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("203.0.113.13"),
            DeviceRole.Router);
        Guid captureId = Guid.NewGuid();
        device.RecordCompletedCapture(captureId);
        await devices.AddAsync(device);

        snapshots.SectionsBySnapshot[captureId] =
        [
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.NetworkInterfaces,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>
                    {
                        ["name"] = "ether1",
                        ["dynamic"] = "false",
                    }),
                ]),
        ];

        ZoneResolveDeviceObservation observation = await source.GetForDeviceAsync(device.Id);
        Assert.True(observation.ObservationAvailable);
        Assert.Empty(observation.ContainerVethEdges);
        Assert.Empty(observation.SharedVethNames);

        ZoneBindingResolveResult plain = ZoneResolveEngine.Resolve(
            NodeZoneBinding.Create(
                node.Id,
                ZoneId.New(),
                NodeZoneBindingKind.SingleInterface,
                ["ether1"],
                Hash256.Create(new byte[32])),
            observation);
        Assert.Equal(["ether1"], plain.ResolvedMembers);

        ZoneBindingResolveResult marker = ZoneResolveEngine.Resolve(
            NodeZoneBinding.Create(
                node.Id,
                ZoneId.New(),
                NodeZoneBindingKind.SingleInterface,
                ["container:pihole"],
                Hash256.Create(new byte[32])),
            observation);
        Assert.Contains(marker.Blockers, b => b.Code == ZoneResolveBlockerCodes.MissingContainer);
        Assert.DoesNotContain(
            marker.Blockers,
            b => b.Code == ZoneResolveBlockerCodes.MissingInterface
                 && string.Equals(b.Subject, "container:pihole", StringComparison.Ordinal));
        Assert.Empty(marker.ResolvedMembers);
    }
}
