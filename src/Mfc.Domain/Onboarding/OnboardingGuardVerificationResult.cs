using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>One management-guard verification finding (Onboarding Spec §58 / M5-03).</summary>
public sealed class OnboardingGuardFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public DeviceId? DeviceId { get; init; }

    public string? Chain { get; init; }

    public int? Ordinal { get; init; }

    public string? Target { get; init; }
}

/// <summary>Aggregate result of <see cref="OnboardingGuardVerifier"/>.</summary>
public sealed class OnboardingGuardVerificationResult
{
    public required IReadOnlyList<OnboardingGuardFinding> Findings { get; init; }

    /// <summary>Canonical hash of the verified <see cref="GuardProfile"/> (AC#9).</summary>
    public required Hash256 GuardHash { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != OnboardingCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == OnboardingCodes.SeverityBlocker);
}
