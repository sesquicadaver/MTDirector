using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Abstractions.Deployment;

/// <summary>
/// Default runtime when no live RouterOS deployment adapter is registered (same pattern as onboarding).
/// </summary>
public sealed class NotConfiguredDeploymentRuntime : IDeploymentRuntime
{
    public const string NotConfiguredMessage =
        "Deployment runtime is not_configured for live RouterOS mutation; inject an adapter for Start/Rollback.";

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
        throw new InvalidOperationException(NotConfiguredMessage);
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
        throw new InvalidOperationException(NotConfiguredMessage);
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
        throw new InvalidOperationException(NotConfiguredMessage);
    }
}
