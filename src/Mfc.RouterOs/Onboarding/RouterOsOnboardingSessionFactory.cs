using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.RouterOs.Onboarding;

/// <summary>Production onboarding session factory using connection profiles + API-SSL (P2-07).</summary>
public sealed class RouterOsOnboardingSessionFactory : IRouterOsOnboardingSessionFactory
{
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsConnectionMaterializer _materializer;

    public RouterOsOnboardingSessionFactory(
        IConnectionProfileReadStore profiles,
        IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(materializer);
        _profiles = profiles;
        _materializer = materializer;
    }

    public async Task<RouterOsOnboardingScopedSessions> OpenAsync(
        Node node,
        OnboardingPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.NodeId != node.Id)
        {
            throw new DomainInvariantException("Onboarding plan node mismatch.");
        }

        List<RouterOsOnboardingDeviceSession> sessions = [];
        foreach (DeviceOnboardingPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
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
            RouterOsOnboardingDeviceSession session = await RouterOsOnboardingDeviceSession
                .OpenAsync(device.Id, target, _materializer, cancellationToken)
                .ConfigureAwait(false);
            sessions.Add(session);
        }

        return new RouterOsOnboardingScopedSessions(sessions, sessions);
    }
}
