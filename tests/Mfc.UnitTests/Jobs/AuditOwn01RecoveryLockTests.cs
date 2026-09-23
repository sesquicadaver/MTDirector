using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Jobs;
using Mfc.Application.Onboarding;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Jobs;

/// <summary>AUDIT-OWN-01: recovery skips live onboarding lease; Start acquires lock and expires after Execute.</summary>
public sealed class AuditOwn01RecoveryLockTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-23T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task RecoverySkipsWhenLiveLockHeldForSameOnboarding()
    {
        Node node = Onboarding.OnboardingTestFactory.RouterWithDevice(out _);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeOnboardingStore onboardings = new();
        FakeClock clock = new() { UtcNow = T0 };

        OnboardingPlan plan = Onboarding.OnboardingTestFactory.PlanFor(node, T0);
        await onboardings.AddPlanAsync(plan);
        OnboardingOperation operation = OnboardingOperation.Create(plan, node, UserId.New(), T0);
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        await onboardings.AddOperationAsync(operation);
        await onboardings.AddLockAsync(
            OnboardingLock.Acquire(node.Id, operation.Id, OnboardingOwnership.DefaultOwnerInstanceId, T0));

        CountingOnboardingRuntime runtime = new();
        RecoverNonterminalOperationsJobUseCase useCase = new(
            new FakeDeploymentStore(),
            onboardings,
            nodes,
            new NoopDeploymentRuntime(),
            runtime,
            clock);

        ApplicationResult<RecoverNonterminalOperationsJobResult> result = await useCase.ExecuteAsync(5);
        Assert.True(result.IsSuccess);
        OperationRecoveryJobItemResult item = Assert.Single(result.Value!.Items);
        Assert.Equal(operation.Id.Value, item.OperationId);
        Assert.True(item.Succeeded);
        Assert.Equal(OnboardingOwnership.RecoverySkippedLockHeld, item.ErrorCode);
        Assert.Equal(0, runtime.RecoverCalls);
        Assert.Equal(OnboardingOperationState.Prechecking, (await onboardings.GetOperationAsync(operation.Id))!.State);
    }

    [Fact]
    public async Task RecoveryRunsWhenLockMissingOrExpired()
    {
        Node node = Onboarding.OnboardingTestFactory.RouterWithDevice(out _);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeOnboardingStore onboardings = new();
        FakeClock clock = new() { UtcNow = T0 };

        OnboardingPlan plan = Onboarding.OnboardingTestFactory.PlanFor(node, T0);
        await onboardings.AddPlanAsync(plan);
        OnboardingOperation withoutLockOp = OnboardingOperation.Create(plan, node, UserId.New(), T0);
        withoutLockOp.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        await onboardings.AddOperationAsync(withoutLockOp);

        CountingOnboardingRuntime runtime = new();
        RecoverNonterminalOperationsJobUseCase useCase = new(
            new FakeDeploymentStore(),
            onboardings,
            nodes,
            new NoopDeploymentRuntime(),
            runtime,
            clock);

        ApplicationResult<RecoverNonterminalOperationsJobResult> withoutLock = await useCase.ExecuteAsync(5);
        Assert.True(withoutLock.IsSuccess);
        Assert.Null(Assert.Single(withoutLock.Value!.Items).ErrorCode);
        Assert.Equal(1, runtime.RecoverCalls);

        OnboardingOperation expiredLockOp = OnboardingOperation.Create(plan, node, UserId.New(), T0);
        expiredLockOp.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(2));
        await onboardings.AddOperationAsync(expiredLockOp);
        OnboardingLock expired = OnboardingLock.Acquire(
            node.Id, expiredLockOp.Id, "owner", T0, lease: TimeSpan.FromSeconds(30));
        expired.Expire(T0.AddMinutes(1));
        await onboardings.AddLockAsync(expired);
        clock.UtcNow = T0.AddMinutes(1);

        ApplicationResult<RecoverNonterminalOperationsJobResult> withExpired = await useCase.ExecuteAsync(5);
        Assert.True(withExpired.IsSuccess);
        Assert.True(runtime.RecoverCalls >= 2);
        Assert.Contains(
            withExpired.Value!.Items,
            static i => i.ErrorCode is null);
    }

    [Fact]
    public async Task StartAcquiresLockAndExpiresLeaseAfterExecute()
    {
        Node node = Onboarding.OnboardingTestFactory.RouterWithDevice(out _);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeOnboardingStore onboardings = new();
        FakeAuthorizationBoundary auth = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = T0 };
        OnboardingPlan plan = Onboarding.OnboardingTestFactory.PlanFor(node, T0);
        await onboardings.AddPlanAsync(plan);

        HoldingOnboardingRuntime holding = new();
        StartOnboardingUseCase start = new(
            auth,
            nodes,
            onboardings,
            idempotency,
            audit,
            clock,
            holding,
            new FakeUnitOfWork());

        Task<ApplicationResult<OnboardingOperationSummaryView>> startTask = start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Id.Value,
                PlanHash = plan.PlanHash.Bytes.ToArray(),
                OwnerInstanceId = "owner-a",
            });

        await holding.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        OnboardingLock? during = await onboardings.GetLockByNodeAsync(node.Id);
        Assert.NotNull(during);
        Assert.False(during!.IsExpired(T0));
        Assert.Equal("owner-a", during.OwnerInstanceId);

        CountingOnboardingRuntime recoverRuntime = new();
        ApplicationResult<RecoverNonterminalOperationsJobResult> recovery = await new RecoverNonterminalOperationsJobUseCase(
                new FakeDeploymentStore(),
                onboardings,
                nodes,
                new NoopDeploymentRuntime(),
                recoverRuntime,
                clock)
            .ExecuteAsync(5);
        Assert.Equal(OnboardingOwnership.RecoverySkippedLockHeld, Assert.Single(recovery.Value!.Items).ErrorCode);
        Assert.Equal(0, recoverRuntime.RecoverCalls);

        holding.Release.SetResult();
        ApplicationResult<OnboardingOperationSummaryView> started = await startTask;
        Assert.True(started.IsSuccess, started.Error?.Message);

        OnboardingLock? after = await onboardings.GetLockByNodeAsync(node.Id);
        Assert.NotNull(after);
        Assert.True(after!.IsExpired(T0));
        Assert.True(after.ExpiresAtUtc >= after.AcquiredAtUtc);
    }

    private sealed class CountingOnboardingRuntime : IOnboardingRuntime
    {
        public int RecoverCalls { get; private set; }

        public Task<OnboardingExecutionResult> ExecuteAsync(
            Node node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            DateTimeOffset routerClock,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRollbackResult> RollbackAsync(
            Node node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRecoveryResult> RecoverAsync(
            Node node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            RecoverCalls++;
            return Task.FromResult(new OnboardingRecoveryResult
            {
                Action = OnboardingRecoveryAction.KeepManaged,
                State = operation.State,
                Timeline = ["ok"],
                NodeUnmanaged = false,
                NodeManaged = true,
            });
        }
    }

    private sealed class HoldingOnboardingRuntime : IOnboardingRuntime
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OnboardingExecutionResult> ExecuteAsync(
            Node node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            DateTimeOffset routerClock,
            CancellationToken cancellationToken = default)
        {
            operation.EnsureTransition(OnboardingOperationState.Prechecking, nowUtc);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            operation.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, nowUtc);
            operation.EnsureTransition(OnboardingOperationState.StagingDisabledAnchors, nowUtc);
            operation.EnsureTransition(OnboardingOperationState.ArmingWatchdogs, nowUtc);
            operation.EnsureTransition(OnboardingOperationState.EnablingAnchors, nowUtc);
            operation.EnsureTransition(OnboardingOperationState.Verifying, nowUtc);
            operation.EnsureTransition(OnboardingOperationState.DisarmingWatchdogs, nowUtc);
            operation.EnsureTransition(OnboardingOperationState.Committed, nowUtc);
            return new OnboardingExecutionResult
            {
                Succeeded = true,
                State = operation.State,
                Timeline = ["execute"],
                CapturePerformed = true,
                WatchdogsDisarmed = true,
                NodeManaged = true,
            };
        }

        public Task<OnboardingRollbackResult> RollbackAsync(
            Node node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRecoveryResult> RecoverAsync(
            Node node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopDeploymentRuntime : IDeploymentRuntime
    {
        public Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
            Node node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            IReadOnlyList<Mfc.Domain.Policy.PacketPathPairFact> packetPathPairs,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWorkflowRollbackResult> RollbackAsync(
            Node node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
            Node node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
