using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Abstractions.Deployment;

/// <summary>
/// Device-session runtime for deployment execute/rollback/recover.
/// Application never talks to RouterOS; Controller injects a RouterOS-backed adapter.
/// </summary>
public interface IDeploymentRuntime
{
    Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        IReadOnlyList<PacketPathPairFact> packetPathPairs,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<DeploymentWorkflowRollbackResult> RollbackAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
