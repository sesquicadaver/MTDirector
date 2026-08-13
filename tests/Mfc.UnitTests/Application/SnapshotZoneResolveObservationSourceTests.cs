using Mfc.Application.Zones;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
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
}
