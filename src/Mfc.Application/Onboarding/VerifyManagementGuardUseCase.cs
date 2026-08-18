using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Onboarding;

/// <summary>
/// Verifies the external management guard for onboarding (M5-03).
/// Read-only: never creates, moves, or rewrites RouterOS guard rules.
/// </summary>
public static class VerifyManagementGuardUseCase
{
    /// <summary>
    /// Runs <see cref="OnboardingGuardVerifier"/> over caller-supplied filter facts and plan hash.
    /// </summary>
    public static OnboardingGuardVerificationResult Execute(
        GuardProfile profile,
        IReadOnlyList<ActualFilterRule> rules,
        Hash256 expectedGuardHash,
        IReadOnlyList<AnchorPlacement>? plannedPlacements = null,
        IReadOnlyList<string>? candidateComments = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(expectedGuardHash);
        return OnboardingGuardVerifier.Verify(
            profile,
            rules,
            expectedGuardHash,
            plannedPlacements,
            candidateComments);
    }
}
