using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Onboarding;

/// <summary>
/// Validates RouterOS onboarding prerequisites for every enabled Node member (M5-02).
/// Does not call RouterOS and never mutates users, services, or device-mode.
/// </summary>
public static class ValidateOnboardingPrerequisitesUseCase
{
    /// <summary>
    /// Runs <see cref="OnboardingPrerequisiteValidator"/> over caller-supplied read-only facts.
    /// </summary>
    public static OnboardingPrerequisiteResult Execute(
        Node node,
        IReadOnlyDictionary<DeviceId, OnboardingDevicePrerequisiteFacts> factsByDevice)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(factsByDevice);
        return OnboardingPrerequisiteValidator.Validate(node, factsByDevice);
    }
}
