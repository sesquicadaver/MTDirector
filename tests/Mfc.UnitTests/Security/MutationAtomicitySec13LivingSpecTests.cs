using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Workflow;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-13 (#394) — UoW for UpsertDeviceHashState.</summary>
public sealed class MutationAtomicitySec13LivingSpecTests
{
    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    [Fact]
    public async Task Ac1UpsertRunsInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeDeviceHashStateStore hashStates = new();
        FakeClock clock = new();
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

        UpsertDeviceHashStateUseCase useCase = new(auth, devices, hashStates, clock, unitOfWork);
        ApplicationResult<DeviceHashStateView> result = await useCase.ExecuteAsync(
            new UpsertDeviceHashStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                DesiredArtifactHashHex = Hash(1).ToString(),
                ActualKnown = true,
                AnchorKnown = true,
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.NotNull(await hashStates.GetAsync(device.Id));
    }

    [Fact]
    public void Ac2SourceUsesUnitOfWorkBoundary()
    {
        string path = Path.Combine(
            FindRepoRoot(), "src", "Mfc.Application", "Workflow", "WorkflowUseCases.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        Assert.Contains("_unitOfWork.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("_hashStates.UpsertAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3KnownLimitationsDocumentsSec13()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-13", source, StringComparison.Ordinal);
        Assert.Contains("UpsertDeviceHashState", source, StringComparison.Ordinal);
        Assert.Contains("SEC-07", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentWorkflow", source, StringComparison.Ordinal);
        Assert.Contains("UpdateConnectionProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "`UpsertDeviceHashStateUseCase` (candidate SEC-13)",
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
