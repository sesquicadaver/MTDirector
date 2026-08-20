using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Deployment;
using Mfc.Application.Drift;
using Mfc.Application.Jobs;
using Mfc.Application.Onboarding;
using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Jobs;

/// <summary>Application-layer coverage for M6-03 operational job use cases.</summary>
public sealed class OperationalJobUseCaseCoverageTests
{
    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    [Fact]
    public async Task HeartbeatRefreshesOwnedNonExpiredLocks()
    {
        FakeDeploymentStore deployments = new();
        FakeClock clock = new() { UtcNow = DateTimeOffset.Parse("2026-08-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture) };
        NodeId nodeId = NodeId.New();
        DeploymentOperationId opId = DeploymentOperationId.New();
        DeploymentLock lockRow = DeploymentLock.Acquire(nodeId, opId, "owner-a", clock.UtcNow);
        await deployments.AddLockAsync(lockRow);

        HeartbeatDeploymentLocksJobUseCase useCase = new(deployments, clock);
        clock.UtcNow = clock.UtcNow.AddSeconds(10);
        var result = await useCase.ExecuteAsync("owner-a");
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.RefreshedCount);

        DeploymentLock? saved = await deployments.GetLockByNodeAsync(nodeId);
        Assert.NotNull(saved);
        Assert.True(saved!.ExpiresAtUtc > clock.UtcNow);
    }

    [Fact]
    public async Task HeartbeatSkipsExpiredLocks()
    {
        FakeDeploymentStore deployments = new();
        FakeClock clock = new() { UtcNow = DateTimeOffset.Parse("2026-08-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture) };
        DeploymentLock lockRow = DeploymentLock.Acquire(NodeId.New(), DeploymentOperationId.New(), "owner-a", clock.UtcNow, lease: TimeSpan.FromSeconds(30));
        await deployments.AddLockAsync(lockRow);
        clock.UtcNow = clock.UtcNow.AddMinutes(5);

        var result = await new HeartbeatDeploymentLocksJobUseCase(deployments, clock).ExecuteAsync("owner-a");
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.RefreshedCount);
    }

    [Fact]
    public void HeartbeatRejectsEmptyOwner()
    {
        HeartbeatDeploymentLocksJobUseCase useCase = new(new FakeDeploymentStore(), new FakeClock());
        Assert.Throws<ArgumentException>(() => useCase.ExecuteAsync("  ").GetAwaiter().GetResult());
    }

    [Fact]
    public async Task PollManagedDriftProcessesDevicesWithLastCommitted()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeDeviceHashStateStore hashes = new();
        FakeDriftEventStore drift = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();

        Device device = Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.10", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Managed,
            rowVersion: 1,
            lastCompletedCaptureId: null);
        await devices.AddAsync(device);
        Hash256 committed = Hash(2);
        await hashes.UpsertAsync(DeviceHashState.Create(
            device.Id, committed, committed, committed, committed, committed,
            actualKnown: true, anchorKnown: true, updatedAtUtc: clock.UtcNow));

        PollManagedDriftJobUseCase useCase = new(
            hashes,
            new DetectManagedDriftUseCase(auth, devices, hashes, drift, audit, clock));
        var result = await useCase.ExecuteAsync("tester", batchSize: 10);
        Assert.True(result.IsSuccess);
        Assert.Contains(device.Id.Value, result.Value!.DeviceIdsPolled);
        Assert.NotEmpty(result.Value.DriftEventIds);
    }

    [Fact]
    public async Task PollManagedDriftRejectsInvalidBatch()
    {
        var result = await new PollManagedDriftJobUseCase(
                new FakeDeviceHashStateStore(),
                new DetectManagedDriftUseCase(
                    new FakeAuthorizationBoundary(),
                    new FakeDeviceStore(),
                    new FakeDeviceHashStateStore(),
                    new FakeDriftEventStore(),
                    new FakeAuditEventWriter(),
                    new FakeClock()))
            .ExecuteAsync("tester", batchSize: 0);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CleanupAllowsOnlyTemporaryWatchdogNames()
    {
        FakeDeviceStore devices = new();
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

        RecordingCleanupPort port = new();
        CleanupDisabledWatchdogResidueJobUseCase useCase = new(port, devices);
        string allowed = "mfc-rb-d-0123456789abcdef";
        var result = await useCase.ExecuteAsync(
            device.Id.Value,
            [allowed, "mfc4.filter.root", "snapshot-x"]);
        Assert.True(result.IsSuccess);
        Assert.Equal([allowed], result.Value!.RemovedNames);
        Assert.Equal(2, result.Value.RejectedNames.Count);
        Assert.Equal(1, port.Calls);
    }

    [Fact]
    public async Task CleanupFailsWhenDeviceMissingForAllowedNames()
    {
        RecordingCleanupPort port = new();
        CleanupDisabledWatchdogResidueJobUseCase useCase = new(port, new FakeDeviceStore());
        var result = await useCase.ExecuteAsync(
            Guid.NewGuid(),
            ["mfc-rb-d-0123456789abcdef"]);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, port.Calls);
    }

    [Fact]
    public async Task RecoverNonterminalHandlesEmptyAndInvalidBatch()
    {
        RecoverNonterminalOperationsJobUseCase useCase = new(
            new FakeDeploymentStore(),
            new FakeOnboardingStore(),
            new FakeNodeStore(),
            new ScriptedDeploymentRuntime(),
            new ScriptedOnboardingRuntime(),
            new FakeClock());

        Assert.False((await useCase.ExecuteAsync(0)).IsSuccess);
        var empty = await useCase.ExecuteAsync(5);
        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Value!.Items);
    }

    [Fact]
    public async Task ReconcileExpiredExceptionsUsesExpireUseCaseWithoutRouterOs()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyApprovalStore approvals = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = DateTimeOffset.Parse("2026-08-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture) };

        ReconcileExpiredExceptionBindingsJobUseCase useCase = new(
            approvals,
            new ExpireExceptionBindingUseCase(auth, approvals, idempotency, audit, clock),
            clock);
        var result = await useCase.ExecuteAsync("system:operational-jobs", 8);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.ExpiredBindingIds);
        Assert.False((await useCase.ExecuteAsync("system:operational-jobs", 0)).IsSuccess);
    }

    private sealed class RecordingCleanupPort : IWatchdogResidueCleanupPort
    {
        public int Calls { get; private set; }

        public Task<WatchdogResidueCleanupResult> RemoveDisabledTemporaryWatchdogResourcesAsync(
            DeviceId deviceId,
            IReadOnlyList<string> candidateNames,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new WatchdogResidueCleanupResult
            {
                Succeeded = true,
                RemovedNames = candidateNames.ToArray(),
            });
        }
    }

    private sealed class ScriptedDeploymentRuntime : IDeploymentRuntime
    {
        public Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
            Node node, DeploymentPlan plan, DeploymentOperation operation,
            IReadOnlyList<Mfc.Domain.Policy.PacketPathPairFact> packetPathPairs,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWorkflowRollbackResult> RollbackAsync(
            Node node, DeploymentPlan plan, DeploymentOperation operation,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
            Node node, DeploymentPlan plan, DeploymentOperation operation,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWorkflowRecoveryResult
            {
                Action = DeploymentRecoveryAction.MarkFailedOrCanceled,
                State = operation.State,
                Timeline = ["ok"],
            });
    }

    private sealed class ScriptedOnboardingRuntime : IOnboardingRuntime
    {
        public Task<OnboardingExecutionResult> ExecuteAsync(
            Node node, OnboardingPlan plan, OnboardingOperation operation,
            DateTimeOffset nowUtc, DateTimeOffset routerClock, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRollbackResult> RollbackAsync(
            Node node, OnboardingPlan plan, OnboardingOperation operation,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRecoveryResult> RecoverAsync(
            Node node, OnboardingPlan plan, OnboardingOperation operation,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new OnboardingRecoveryResult
            {
                Action = OnboardingRecoveryAction.KeepManaged,
                State = operation.State,
                Timeline = ["ok"],
                NodeUnmanaged = false,
                NodeManaged = true,
            });
    }
}
