using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Append-only deployment plans, mutable operations, device states, locks, and write-ahead steps (M4-01).</summary>
public interface IDeploymentStore
{
    Task AddPlanAsync(DeploymentPlan plan, CancellationToken cancellationToken = default);

    Task<DeploymentPlan?> GetPlanAsync(DeploymentPlanId id, CancellationToken cancellationToken = default);

    Task AddOperationAsync(DeploymentOperation operation, CancellationToken cancellationToken = default);

    Task SaveOperationAsync(DeploymentOperation operation, CancellationToken cancellationToken = default);

    Task<DeploymentOperation?> GetOperationAsync(
        DeploymentOperationId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeploymentOperation>> ListNonterminalByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);

    Task AddDeviceStateAsync(DeviceDeployment device, CancellationToken cancellationToken = default);

    Task SaveDeviceStateAsync(DeviceDeployment device, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceDeployment>> ListDeviceStatesAsync(
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default);

    Task AddStepAsync(DeploymentStep deploymentStep, CancellationToken cancellationToken = default);

    Task SaveStepAsync(DeploymentStep deploymentStep, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeploymentStep>> ListStepsAsync(
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default);

    Task AddLockAsync(DeploymentLock deploymentLock, CancellationToken cancellationToken = default);

    Task SaveLockAsync(DeploymentLock deploymentLock, CancellationToken cancellationToken = default);

    Task<DeploymentLock?> GetLockByNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default);
}
