using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Snapshots;

/// <summary>W6-03 + AUDIT-CAP-04: StartCapture node_id Application fan-out.</summary>
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
            CreateDeviceCapture(new FakeDeviceStore(), new FakeSnapshotCapturePort()));

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
            CreateDeviceCapture(new FakeDeviceStore(), new FakeSnapshotCapturePort()));

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

    [Fact]
    public async Task ContinuesAfterMemberFailureAndReportsUnfitTimeSet()
    {
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("pair"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.One);
        Device ok = Device.Reconstitute(
            DeviceId.New(),
            node.Id,
            NonEmptyName.Create("a-ok"),
            ManagementEndpoint.Create("192.0.2.10"),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1);
        Device bad = Device.Reconstitute(
            DeviceId.New(),
            node.Id,
            NonEmptyName.Create("b-bad"),
            ManagementEndpoint.Create("192.0.2.11"),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1);

        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeDeviceStore devices = new();
        await devices.AddAsync(ok);
        await devices.AddAsync(bad);

        FakeConnectionProfileReadStore profiles = new();
        profiles.ByDevice[ok.Id.Value] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };
        profiles.ByDevice[bad.Id.Value] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };

        FakeSnapshotCapturePort capture = new();
        capture.FailDeviceIds.Add(bad.Id.Value);
        CaptureSnapshotUseCase deviceCapture = new(
            new FakeAuthorizationBoundary(),
            devices,
            profiles,
            capture,
            new FakeSnapshotStore(),
            new FakeAuditEventWriter(),
            new FakeUnitOfWork());
        CaptureNodeSnapshotsUseCase useCase = new(
            new FakeAuthorizationBoundary(),
            nodes,
            devices,
            deviceCapture);

        ApplicationResult<CaptureNodeSnapshotsView> result = await useCase.ExecuteAsync(
            new CaptureNodeSnapshotsCommand
            {
                Actor = "tester",
                NodeId = node.Id.Value,
                IdempotencyKey = Guid.NewGuid(),
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Members.Count);
        Assert.Equal(2, capture.CaptureCount);
        Assert.False(result.Value.AllMembersSucceeded);
        Assert.False(result.Value.TimeSetFit);
        Assert.Contains(result.Value.Members, m => m.Snapshot is not null);
        Assert.Contains(result.Value.Members, m => m.ErrorCode is not null);
    }

    [Fact]
    public void EvaluateTimeSetFitRejectsSkewBeyondMax()
    {
        DateTimeOffset t0 = DateTimeOffset.Parse("2026-09-24T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        CaptureNodeMemberSnapshotView[] members =
        [
            new()
            {
                DeviceId = Guid.NewGuid(),
                DisplayName = "a",
                Snapshot = new SnapshotView
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    Status = SnapshotStatus.Completed,
                    CompletedAtUtc = t0,
                },
            },
            new()
            {
                DeviceId = Guid.NewGuid(),
                DisplayName = "b",
                Snapshot = new SnapshotView
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    Status = SnapshotStatus.Completed,
                    CompletedAtUtc = t0 + CaptureNodeSnapshotsUseCase.MaxMemberCaptureSkew + TimeSpan.FromSeconds(1),
                },
            },
        ];

        Assert.False(CaptureNodeSnapshotsUseCase.EvaluateTimeSetFit(members, out TimeSpan? skew));
        Assert.NotNull(skew);
        Assert.True(skew > CaptureNodeSnapshotsUseCase.MaxMemberCaptureSkew);
    }

    private static CaptureSnapshotUseCase CreateDeviceCapture(
        FakeDeviceStore devices,
        FakeSnapshotCapturePort capture)
        => new(
            new FakeAuthorizationBoundary(),
            devices,
            new FakeConnectionProfileReadStore(),
            capture,
            new FakeSnapshotStore(),
            new FakeAuditEventWriter(),
            new FakeUnitOfWork());
}
