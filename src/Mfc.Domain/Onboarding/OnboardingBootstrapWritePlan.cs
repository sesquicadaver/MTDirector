namespace Mfc.Domain.Onboarding;

/// <summary>One bootstrap-write planning finding (Onboarding Spec §58 / M5-05).</summary>
public sealed class OnboardingBootstrapWriteFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Target { get; init; }
}

/// <summary>Outcome of <see cref="OnboardingBootstrapWritePlanner"/>.</summary>
public sealed class OnboardingBootstrapWritePlan
{
    public required IReadOnlyList<OnboardingBootstrapWriteFinding> Findings { get; init; }

    public required IReadOnlyList<OnboardingBootstrapWrite> Writes { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != OnboardingCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == OnboardingCodes.SeverityBlocker);
}
