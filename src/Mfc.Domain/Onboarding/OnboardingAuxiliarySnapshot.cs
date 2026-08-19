using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Content hashes of non-filter surfaces that onboarding must not mutate (Onboarding Spec §40.14–§40.17).
/// </summary>
public sealed class OnboardingAuxiliarySnapshot
{
    public required Hash256 NatHash { get; init; }

    public required Hash256 RawHash { get; init; }

    public required Hash256 MangleHash { get; init; }

    public required Hash256 RoutingHash { get; init; }

    public required Hash256 VrrpHash { get; init; }

    public required Hash256 InterfaceListHash { get; init; }

    public bool EqualsSnapshot(OnboardingAuxiliarySnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return NatHash.Equals(other.NatHash)
               && RawHash.Equals(other.RawHash)
               && MangleHash.Equals(other.MangleHash)
               && RoutingHash.Equals(other.RoutingHash)
               && VrrpHash.Equals(other.VrrpHash)
               && InterfaceListHash.Equals(other.InterfaceListHash);
    }
}
