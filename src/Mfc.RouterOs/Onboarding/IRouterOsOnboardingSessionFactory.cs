using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;

namespace Mfc.RouterOs.Onboarding;

/// <summary>Opens scoped live onboarding sessions for a node plan (P2-07).</summary>
public interface IRouterOsOnboardingSessionFactory
{
    Task<RouterOsOnboardingScopedSessions> OpenAsync(
        Node node,
        OnboardingPlan plan,
        CancellationToken cancellationToken = default);
}
