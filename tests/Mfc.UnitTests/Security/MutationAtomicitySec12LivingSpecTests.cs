using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-12 (#392) — UoW for CaptureSnapshot persist+audit.</summary>
public sealed class MutationAtomicitySec12LivingSpecTests
{
    [Fact]
    public async Task Ac1CaptureCompletedPersistsAndAuditsInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeSnapshotCapturePort capture = new();
        FakeSnapshotStore snapshots = new();
        FakeAuditEventWriter audit = new();
        SpyUnitOfWork unitOfWork = new();

        Device device = Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.11", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Managed,
            rowVersion: 1,
            lastCompletedCaptureId: null);
        await devices.AddAsync(device);
        profiles.ByDevice[device.Id.Value] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };
        capture.NextResult = FakeSnapshotCapturePort.CreateResult(Enumerable.Repeat((byte)7, 32).ToArray());

        CaptureSnapshotUseCase useCase = new(
            auth, devices, profiles, capture, snapshots, audit, unitOfWork);
        ApplicationResult<SnapshotView> result = await useCase.ExecuteAsync(
            new CaptureSnapshotCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                IdempotencyKey = Guid.NewGuid(),
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.Contains(audit.Events, e => e.Action == "snapshot.capture.completed");
    }

    [Fact]
    public void Ac2SourceUsesUnitOfWorkAndCapturePortStaysOutside()
    {
        string path = Path.Combine(
            FindRepoRoot(), "src", "Mfc.Application", "Snapshots", "CaptureSnapshotUseCase.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        Assert.Contains("_unitOfWork.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("PersistCompletedAsync", source, StringComparison.Ordinal);
        int captureIdx = source.IndexOf("_capture.CaptureAsync", StringComparison.Ordinal);
        int persistIdx = source.IndexOf("PersistCompletedAsync", StringComparison.Ordinal);
        Assert.True(captureIdx >= 0 && persistIdx > captureIdx);
    }

    [Fact]
    public void Ac3KnownLimitationsDocumentsSec12()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-12", source, StringComparison.Ordinal);
        Assert.Contains("CaptureSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("SEC-07", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentWorkflow", source, StringComparison.Ordinal);
        Assert.Contains("UpdateConnectionProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "`CaptureSnapshotUseCase` persist+audit (candidate SEC-12)",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return action(cancellationToken);
        }
    }
}
