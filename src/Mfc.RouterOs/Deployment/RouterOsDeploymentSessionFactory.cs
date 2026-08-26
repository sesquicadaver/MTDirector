using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.RouterOs.Deployment;

/// <summary>Production deployment session factory using connection profiles + API-SSL (P2-08).</summary>
public sealed class RouterOsDeploymentSessionFactory : IRouterOsDeploymentSessionFactory
{
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsConnectionMaterializer _materializer;

    public RouterOsDeploymentSessionFactory(
        IConnectionProfileReadStore profiles,
        IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(materializer);
        _profiles = profiles;
        _materializer = materializer;
    }

    public async Task<RouterOsDeploymentScopedSessions> OpenAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.NodeId != node.Id)
        {
            throw new DomainInvariantException("Deployment plan node mismatch.");
        }

        List<IDeploymentLiveDeviceSession> sessions = [];
        foreach (DeviceDeploymentPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
        {
            Device? device = node.Devices.FirstOrDefault(d => d.Id == devicePlan.DeviceId && d.Enabled);
            if (device is null)
            {
                throw new DomainInvariantException(
                    $"Enabled device '{devicePlan.DeviceId}' is missing on node '{node.Id}'.");
            }

            ConnectionProfileReadModel? profile = await _profiles.GetAsync(device.Id, cancellationToken)
                .ConfigureAwait(false);
            if (profile is null)
            {
                throw new InvalidOperationException(
                    $"Connection profile for device '{device.Id}' is missing.");
            }

            RouterOsReadTarget target = new()
            {
                DeviceId = device.Id,
                Endpoint = device.ManagementEndpoint,
                SecretReference = profile.SecretReference,
                TrustMode = profile.TrustMode,
                CaProfileRef = profile.CaProfileRef,
                PinnedSpkiSha256 = profile.PinnedSpkiSha256,
            };
            RouterOsDeploymentDeviceSession session = await RouterOsDeploymentDeviceSession
                .OpenAsync(device.Id, devicePlan, operationId, target, _materializer, cancellationToken)
                .ConfigureAwait(false);
            sessions.Add(session);
        }

        return new RouterOsDeploymentScopedSessions(sessions);
    }
}
