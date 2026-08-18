using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>One prerequisite finding with a Spec §58 stable code (M5-02 AC#12).</summary>
public sealed class OnboardingPrerequisiteFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public DeviceId? DeviceId { get; init; }

    public string? Target { get; init; }
}

/// <summary>Aggregate Node-level prerequisite validation result (M5-02).</summary>
public sealed class OnboardingPrerequisiteResult
{
    public required IReadOnlyList<OnboardingPrerequisiteFinding> Findings { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != OnboardingCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == OnboardingCodes.SeverityBlocker);
}
