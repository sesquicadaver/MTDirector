namespace Mfc.Domain.Onboarding.Primitives;

/// <summary>Immutable onboarding plan identity (Onboarding Spec §25).</summary>
public readonly record struct OnboardingPlanId(Guid Value)
{
    public static OnboardingPlanId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Onboarding operation identity (Onboarding Spec §5).</summary>
public readonly record struct OnboardingOperationId(Guid Value)
{
    public static OnboardingOperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Write-ahead journal step identity (Onboarding Spec §54).</summary>
public readonly record struct OnboardingStepId(Guid Value)
{
    public static OnboardingStepId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
