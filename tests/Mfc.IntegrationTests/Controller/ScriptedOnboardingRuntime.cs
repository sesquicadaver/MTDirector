using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using DomainState = Mfc.Domain.Onboarding.OnboardingOperationState;

namespace Mfc.IntegrationTests.Controller;

/// <summary>Test-only onboarding runtime that walks the operation SM without RouterOS I/O.</summary>
public sealed class ScriptedOnboardingRuntime : IOnboardingRuntime
{
    public bool Commit { get; init; } = true;

    public Task<OnboardingExecutionResult> ExecuteAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        DateTimeOffset routerClock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        operation.EnsureTransition(DomainState.Prechecking, nowUtc);
        operation.EnsureTransition(DomainState.StagingBootstrapRoots, nowUtc);
        if (!Commit)
        {
            operation.EnsureTransition(DomainState.RollbackPending, nowUtc);
            return Task.FromResult(new OnboardingExecutionResult
            {
                Succeeded = false,
                State = operation.State,
                Timeline = ["rollback-pending"],
                CapturePerformed = false,
                WatchdogsDisarmed = false,
                NodeManaged = false,
            });
        }

        operation.EnsureTransition(DomainState.StagingDisabledAnchors, nowUtc);
        operation.EnsureTransition(DomainState.ArmingWatchdogs, nowUtc);
        operation.EnsureTransition(DomainState.EnablingAnchors, nowUtc);
        operation.EnsureTransition(DomainState.Verifying, nowUtc);
        operation.EnsureTransition(DomainState.DisarmingWatchdogs, nowUtc);
        operation.EnsureTransition(DomainState.Committed, nowUtc);
        return Task.FromResult(new OnboardingExecutionResult
        {
            Succeeded = true,
            State = operation.State,
            Timeline = ["committed"],
            CapturePerformed = true,
            WatchdogsDisarmed = true,
            NodeManaged = false,
        });
    }

    public Task<OnboardingRollbackResult> RollbackAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (operation.State == DomainState.Created)
        {
            operation.EnsureTransition(DomainState.Prechecking, nowUtc);
            operation.EnsureTransition(DomainState.StagingBootstrapRoots, nowUtc);
            operation.EnsureTransition(DomainState.RollbackPending, nowUtc);
        }

        if (operation.State == DomainState.RollbackPending)
        {
            operation.EnsureTransition(DomainState.RollingBack, nowUtc);
        }

        if (operation.State == DomainState.RollingBack)
        {
            operation.EnsureTransition(DomainState.RolledBack, nowUtc);
        }

        node.SetManagementState(ManagementState.Unmanaged);
        return Task.FromResult(new OnboardingRollbackResult
        {
            Succeeded = operation.State == DomainState.RolledBack,
            State = operation.State,
            Timeline = ["rolled-back"],
            WatchdogsCleaned = true,
            NodeUnmanaged = true,
            RemainingEnabledAnchors = false,
        });
    }

    public Task<OnboardingRecoveryResult> RecoverAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new OnboardingRecoveryResult
        {
            Action = OnboardingRecoveryAction.CleanupRolledBack,
            State = operation.State,
            Timeline = [],
            NodeUnmanaged = node.ManagementState == ManagementState.Unmanaged,
            NodeManaged = node.ManagementState == ManagementState.Managed,
        });
    }
}
