using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only DeploymentService client (ADR 0005 / M4-12).</summary>
public interface IDeploymentServiceClient
{
    Task<DeploymentPlanSummary> CreatePlanAsync(
        Guid nodeId,
        Sha256 logicalPolicyHash,
        Sha256 analysisBundleHash,
        Sha256 topologyHash,
        IReadOnlyList<DeploymentDevicePlanInput> devices,
        CancellationToken cancellationToken = default);

    Task<DeploymentOperationSummary> StartAsync(
        Guid planId,
        Sha256 planHash,
        IReadOnlyList<DeploymentPacketPathPairFact> packetPathPairs,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<DeploymentProgress> WatchAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<DeploymentOperationSummary> RollbackAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<DeploymentRecoveryStatus> GetRecoveryStatusAsync(
        Guid nodeId,
        Guid? operationId = null,
        CancellationToken cancellationToken = default);
}
