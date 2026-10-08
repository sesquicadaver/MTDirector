using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Behavioral coverage for M7-PRES-01 capture → OpenEndpointPresence wire.</summary>
public sealed class EndpointPresenceCaptureProjectionTests
{
    private const string SystemActor = "system:operational-jobs";

    [Fact]
    public async Task ProjectFromCaptureOpensPresenceForManagementIpWithInventoryAnchors()
    {
        (Node node, Device device, EndpointPresenceCaptureProjectionPort port, FakeEndpointPresenceStore presence) =
            await CreateProjectorAsync("192.0.2.50");

        EndpointPresenceCaptureProjectionResult result = await port.ProjectFromCapturePayloadsAsync(
            device.Id,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded);
        EndpointId endpointId = EndpointPresenceCaptureProjectionPort.DeterministicEndpointId(
            device.Id, "ipv4", "192.0.2.50");
        EndpointPresenceInterval? active = await presence.GetActiveIntervalAsync(endpointId);
        Assert.NotNull(active);
        Assert.Equal("192.0.2.50", active!.SourceAddress);
        Assert.Equal(node.SiteId, active.SiteId);
        Assert.Equal(node.Id, active.NodeId);
        Assert.Equal(device.Id, active.DeviceId);
        Assert.Equal(EndpointAttributionCertainty.Unknown, active.AttributionCertainty);
    }

    [Fact]
    public async Task SecondProjectionWithSameAnchorsIsIdempotentSkip()
    {
        (_, Device device, EndpointPresenceCaptureProjectionPort port, FakeEndpointPresenceStore presence) =
            await CreateProjectorAsync("192.0.2.51");

        Assert.True((await port.ProjectFromCapturePayloadsAsync(
            device.Id, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty)).Succeeded);
        Assert.True((await port.ProjectFromCapturePayloadsAsync(
            device.Id, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty)).Succeeded);

        EndpointId endpointId = EndpointPresenceCaptureProjectionPort.DeterministicEndpointId(
            device.Id, "ipv4", "192.0.2.51");
        Assert.NotNull(await presence.GetActiveIntervalAsync(endpointId));
    }

    [Fact]
    public async Task DnsManagementHostSkipsProjection()
    {
        (_, Device device, EndpointPresenceCaptureProjectionPort port, _) =
            await CreateProjectorAsync("core.lab.example");

        EndpointPresenceCaptureProjectionResult result = await port.ProjectFromCapturePayloadsAsync(
            device.Id,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty);

        Assert.False(result.Succeeded);
        Assert.Equal(EndpointPresenceCaptureProjectionPort.ManagementHostNotIpCode, result.ErrorCode);
    }

    [Fact]
    public async Task CaptureSnapshotSucceedsEvenWhenPresenceProjectionFails()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("edge"),
            NodeKind.Router,
            DeclaredUplinkMode.One);
        Device device = Device.Reconstitute(
            DeviceId.New(),
            node.Id,
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.60"),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1);

        FakeDeviceStore devices = new();
        await devices.AddAsync(device);
        FakeConnectionProfileReadStore profiles = new();
        profiles.ByDevice[device.Id.Value] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };

        CaptureSnapshotUseCase useCase = new(
            new FakeAuthorizationBoundary(),
            devices,
            profiles,
            new FakeSnapshotCapturePort(),
            new FakeSnapshotStore(),
            new FakeAuditEventWriter(),
            new FakeUnitOfWork(),
            routingProjection: new NotConfiguredRoutingAssuranceCaptureProjectionPort(),
            presenceProjection: new NotConfiguredEndpointPresenceCaptureProjectionPort());

        ApplicationResult<SnapshotView> captured = await useCase.ExecuteAsync(
            new CaptureSnapshotCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                IdempotencyKey = Guid.NewGuid(),
            });

        Assert.True(captured.IsSuccess);
        Assert.NotNull(captured.Value);
    }

    [Fact]
    public void DeterministicEndpointIdIsStablePerDeviceFamilyAndIp()
    {
        DeviceId deviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        EndpointId a = EndpointPresenceCaptureProjectionPort.DeterministicEndpointId(deviceId, "ipv4", "10.0.0.1");
        EndpointId b = EndpointPresenceCaptureProjectionPort.DeterministicEndpointId(deviceId, "ipv4", "10.0.0.1");
        EndpointId c = EndpointPresenceCaptureProjectionPort.DeterministicEndpointId(deviceId, "ipv4", "10.0.0.2");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    private static async Task<(
        Node Node,
        Device Device,
        EndpointPresenceCaptureProjectionPort Port,
        FakeEndpointPresenceStore Presence)> CreateProjectorAsync(string managementHost)
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("core"),
            NodeKind.Router,
            DeclaredUplinkMode.One);
        Device device = Device.Reconstitute(
            DeviceId.New(),
            node.Id,
            NonEmptyName.Create("router"),
            ManagementEndpoint.Create(managementHost),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1);

        FakeDeviceStore devices = new();
        await devices.AddAsync(device);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeEndpointPresenceStore presence = new();
        OpenEndpointPresenceUseCase open = EndpointPresenceTestKit.CreateOpenUseCase(presence: presence);
        EndpointPresenceCaptureProjectionPort port = new(
            open,
            devices,
            nodes,
            presence,
            SystemActor);
        return (node, device, port, presence);
    }
}
