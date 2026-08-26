using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;

namespace Mfc.RouterOs.Deployment;

/// <summary>Opens scoped live deployment sessions for a node plan (P2-08).</summary>
public interface IRouterOsDeploymentSessionFactory
{
    Task<RouterOsDeploymentScopedSessions> OpenAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default);
}
