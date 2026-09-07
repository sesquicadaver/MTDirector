using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.IntegrationTests.Controller;

/// <summary>Test-only deployment runtime that walks the operation SM without RouterOS I/O.</summary>
public sealed class ScriptedDeploymentRuntime : IDeploymentRuntime
{
    public bool Commit { get; init; } = true;

    public Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        IReadOnlyList<PacketPathPairFact> packetPathPairs,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(packetPathPairs);
        cancellationToken.ThrowIfCancellationRequested();

        Advance(operation, DomainState.Prechecking, nowUtc);
        Advance(operation, DomainState.Staging, nowUtc);
        Advance(operation, DomainState.Staged, nowUtc);
        Advance(operation, DomainState.ArmingWatchdog, nowUtc);
        Advance(operation, DomainState.WatchdogArmed, nowUtc);
        Advance(operation, DomainState.Activating, nowUtc);

        if (Commit)
        {
            Advance(operation, DomainState.Verifying, nowUtc);
            Advance(operation, DomainState.DisarmingWatchdog, nowUtc);
            Advance(operation, DomainState.Committed, nowUtc);
            return Task.FromResult(new DeploymentWorkflowExecutionResult
            {
                Succeeded = true,
                State = operation.State,
                Timeline = ["execute", "committed"],
                ActivationStarted = true,
            });
        }

        Advance(operation, DomainState.RollbackPending, nowUtc);
        return Task.FromResult(new DeploymentWorkflowExecutionResult
        {
            Succeeded = false,
            State = operation.State,
            Timeline = ["execute", "rollback-pending"],
            ActivationStarted = true,
        });
    }

    public Task<DeploymentWorkflowRollbackResult> RollbackAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        if (operation.State is DomainState.Activating or DomainState.Verifying or DomainState.DisarmingWatchdog)
        {
            Advance(operation, DomainState.RollbackPending, nowUtc);
        }

        if (operation.State == DomainState.Created)
        {
            Advance(operation, DomainState.Prechecking, nowUtc);
            Advance(operation, DomainState.Staging, nowUtc);
            Advance(operation, DomainState.RollbackPending, nowUtc);
        }

        if (operation.State == DomainState.RollbackPending)
        {
            Advance(operation, DomainState.RollingBack, nowUtc);
        }

        if (operation.State == DomainState.RollingBack)
        {
            Advance(operation, DomainState.RolledBack, nowUtc);
        }

        return Task.FromResult(new DeploymentWorkflowRollbackResult
        {
            Succeeded = operation.State == DomainState.RolledBack,
            State = operation.State,
            Timeline = ["rollback"],
        });
    }

    public Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DeploymentWorkflowRecoveryResult
        {
            Action = DeploymentRecoveryAction.MarkFailedOrCanceled,
            State = operation.State,
            Timeline = [],
        });
    }

    private static void Advance(DeploymentOperation operation, DomainState next, DateTimeOffset nowUtc)
        => operation.EnsureTransition(next, nowUtc);
}
