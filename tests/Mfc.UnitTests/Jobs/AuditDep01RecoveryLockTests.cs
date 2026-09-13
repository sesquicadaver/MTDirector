using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Jobs;
using Mfc.Application.Onboarding;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Xunit;

namespace Mfc.UnitTests.Jobs;

/// <summary>AUDIT-DEP-01: recovery skips live owner lease; Start acquires lock + IntentRecorded journal.</summary>
public sealed class AuditDep01RecoveryLockTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-13T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task RecoverySkipsWhenLiveLockHeldForSameDeployment()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out Device device);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeDeploymentStore deployments = new();
        FakeClock clock = new() { UtcNow = T0 };

        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        await deployments.AddPlanAsync(plan);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        await deployments.AddOperationAsync(operation);
        await deployments.AddLockAsync(
            DeploymentLock.Acquire(node.Id, operation.Id, DeploymentOwnership.DefaultOwnerInstanceId, T0));

        CountingDeploymentRuntime runtime = new();
        RecoverNonterminalOperationsJobUseCase useCase = new(
            deployments,
            new FakeOnboardingStore(),
            nodes,
            runtime,
            new ScriptedOnboardingRuntime(),
            clock);

        ApplicationResult<RecoverNonterminalOperationsJobResult> result = await useCase.ExecuteAsync(5);
        Assert.True(result.IsSuccess);
        OperationRecoveryJobItemResult item = Assert.Single(result.Value!.Items);
        Assert.Equal(operation.Id.Value, item.OperationId);
        Assert.True(item.Succeeded);
        Assert.Equal(DeploymentOwnership.RecoverySkippedLockHeld, item.ErrorCode);
        Assert.Equal(0, runtime.RecoverCalls);
        Assert.Equal(DeploymentOperationState.Prechecking, (await deployments.GetOperationAsync(operation.Id))!.State);
    }

    [Fact]
    public async Task RecoveryRunsWhenLockMissingOrExpired()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeDeploymentStore deployments = new();
        FakeClock clock = new() { UtcNow = T0 };

        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        await deployments.AddPlanAsync(plan);
        DeploymentOperation withoutLockOp = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        withoutLockOp.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        await deployments.AddOperationAsync(withoutLockOp);

        CountingDeploymentRuntime runtime = new();
        RecoverNonterminalOperationsJobUseCase useCase = new(
            deployments,
            new FakeOnboardingStore(),
            nodes,
            runtime,
            new ScriptedOnboardingRuntime(),
            clock);

        ApplicationResult<RecoverNonterminalOperationsJobResult> withoutLock = await useCase.ExecuteAsync(5);
        Assert.True(withoutLock.IsSuccess);
        Assert.Null(Assert.Single(withoutLock.Value!.Items).ErrorCode);
        Assert.Equal(1, runtime.RecoverCalls);

        DeploymentOperation expiredLockOp = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        expiredLockOp.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(2));
        await deployments.AddOperationAsync(expiredLockOp);
        DeploymentLock expired = DeploymentLock.Acquire(
            node.Id, expiredLockOp.Id, "owner", T0, lease: TimeSpan.FromSeconds(30));
        expired.Expire(T0.AddMinutes(1));
        await deployments.AddLockAsync(expired);
        clock.UtcNow = T0.AddMinutes(1);

        ApplicationResult<RecoverNonterminalOperationsJobResult> withExpired = await useCase.ExecuteAsync(5);
        Assert.True(withExpired.IsSuccess);
        Assert.True(runtime.RecoverCalls >= 2);
        Assert.Contains(
            withExpired.Value!.Items,
            static i => i.ErrorCode is null);
    }

    [Fact]
    public async Task StartAcquiresLockWritesIntentJournalAndExpiresLeaseAfterExecute()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        FakeNodeStore nodes = new();
        await nodes.AddAsync(node);
        FakeDeploymentStore deployments = new();
        FakeAuthorizationBoundary auth = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = T0 };
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        await deployments.AddPlanAsync(plan);

        HoldingDeploymentRuntime holding = new();
        StartDeploymentUseCase start = new(
            auth,
            nodes,
            deployments,
            new FakeDriftEventStore(),
            idempotency,
            audit,
            clock,
            holding,
            new FakeUnitOfWork());

        Task<ApplicationResult<DeploymentOperationSummaryView>> startTask = start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Id.Value,
                PlanHash = plan.PlanHash.Bytes.ToArray(),
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
                OwnerInstanceId = "owner-a",
            });

        await holding.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        DeploymentLock? during = await deployments.GetLockByNodeAsync(node.Id);
        Assert.NotNull(during);
        Assert.False(during!.IsExpired(T0));
        Assert.Equal("owner-a", during.OwnerInstanceId);

        IReadOnlyList<DeploymentStep> steps = await deployments.ListStepsAsync(
            new DeploymentOperationId(during.DeploymentId.Value));
        Assert.NotEmpty(steps);
        Assert.All(steps, static s => Assert.Equal(DeploymentStepState.IntentRecorded, s.State));
        Assert.All(steps, static s => Assert.Equal(DeploymentStepKind.Precheck, s.Kind));

        CountingDeploymentRuntime recoverRuntime = new();
        ApplicationResult<RecoverNonterminalOperationsJobResult> recovery = await new RecoverNonterminalOperationsJobUseCase(
                deployments,
                new FakeOnboardingStore(),
                nodes,
                recoverRuntime,
                new ScriptedOnboardingRuntime(),
                clock)
            .ExecuteAsync(5);
        Assert.Equal(DeploymentOwnership.RecoverySkippedLockHeld, Assert.Single(recovery.Value!.Items).ErrorCode);
        Assert.Equal(0, recoverRuntime.RecoverCalls);

        holding.Release.SetResult();
        ApplicationResult<DeploymentOperationSummaryView> started = await startTask;
        Assert.True(started.IsSuccess, started.Error?.Message);

        DeploymentLock? after = await deployments.GetLockByNodeAsync(node.Id);
        Assert.NotNull(after);
        Assert.True(after!.IsExpired(T0));
        Assert.True(after.ExpiresAtUtc >= after.AcquiredAtUtc);
    }

    private sealed class CountingDeploymentRuntime : IDeploymentRuntime
    {
        public int RecoverCalls { get; private set; }

        public Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
            Node node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            IReadOnlyList<PacketPathPairFact> packetPathPairs,
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
        {
            RecoverCalls++;
            return Task.FromResult(new DeploymentWorkflowRecoveryResult
            {
                Action = DeploymentRecoveryAction.MarkFailedOrCanceled,
                State = operation.State,
                Timeline = ["ok"],
            });
        }
    }

    private sealed class HoldingDeploymentRuntime : IDeploymentRuntime
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
            Node node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            IReadOnlyList<PacketPathPairFact> packetPathPairs,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            operation.EnsureTransition(DeploymentOperationState.Prechecking, nowUtc);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            operation.EnsureTransition(DeploymentOperationState.Staging, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.Staged, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.ArmingWatchdog, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.WatchdogArmed, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.Activating, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.Verifying, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.DisarmingWatchdog, nowUtc);
            operation.EnsureTransition(DeploymentOperationState.Committed, nowUtc);
            return new DeploymentWorkflowExecutionResult
            {
                Succeeded = true,
                State = operation.State,
                Timeline = ["execute"],
                ActivationStarted = true,
            };
        }

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

    private sealed class ScriptedOnboardingRuntime : IOnboardingRuntime
    {
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
