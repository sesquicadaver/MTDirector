using Mfc.Application.Common;
using Mfc.Application.Snapshots;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Snapshots;

/// <summary>W6-03: StartCapture node_id Application fan-out.</summary>
public sealed class CaptureNodeSnapshotsUseCaseTests
{
    [Fact]
    public void DeriveDeviceIdempotencyKeyIsStableAndDistinctPerDevice()
    {
        Guid batch = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid a = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid b = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Guid keyA1 = CaptureNodeSnapshotsUseCase.DeriveDeviceIdempotencyKey(batch, a);
        Guid keyA2 = CaptureNodeSnapshotsUseCase.DeriveDeviceIdempotencyKey(batch, a);
        Guid keyB = CaptureNodeSnapshotsUseCase.DeriveDeviceIdempotencyKey(batch, b);

        Assert.Equal(keyA1, keyA2);
        Assert.NotEqual(keyA1, keyB);
    }

    [Fact]
    public async Task EmptyNodeReturnsValidation()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("empty"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.One);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        CaptureNodeSnapshotsUseCase useCase = new(
            new FakeAuthorizationBoundary(),
            nodes,
            new FakeDeviceStore(),
            CreateDeviceCapture(new FakeDeviceStore()));

        ApplicationResult<CaptureNodeSnapshotsView> result = await useCase.ExecuteAsync(
            new CaptureNodeSnapshotsCommand
            {
                Actor = "tester",
                NodeId = node.Id.Value,
                IdempotencyKey = Guid.NewGuid(),
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public async Task UnknownNodeReturnsNotFound()
    {
        CaptureNodeSnapshotsUseCase useCase = new(
            new FakeAuthorizationBoundary(),
            new FakeNodeStore(),
            new FakeDeviceStore(),
            CreateDeviceCapture(new FakeDeviceStore()));

        ApplicationResult<CaptureNodeSnapshotsView> result = await useCase.ExecuteAsync(
            new CaptureNodeSnapshotsCommand
            {
                Actor = "tester",
                NodeId = Guid.NewGuid(),
                IdempotencyKey = Guid.NewGuid(),
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("not_found", result.Error!.Code);
    }

    private static CaptureSnapshotUseCase CreateDeviceCapture(FakeDeviceStore devices)
        => new(
            new FakeAuthorizationBoundary(),
            devices,
            new FakeConnectionProfileReadStore(),
            new FakeSnapshotCapturePort(),
            new FakeSnapshotStore(),
            new FakeAuditEventWriter(),
            new FakeUnitOfWork());
}
