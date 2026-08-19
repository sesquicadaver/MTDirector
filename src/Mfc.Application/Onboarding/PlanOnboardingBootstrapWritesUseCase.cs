using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Onboarding;

/// <summary>
/// Plans closed bootstrap writes (M5-05). Execution is <see cref="IOnboardingBootstrapWritePort"/>;
/// this use case never talks to RouterOS.
/// </summary>
public static class PlanOnboardingBootstrapWritesUseCase
{
    public static OnboardingBootstrapWritePlan Execute(
        DeviceOnboardingPlan devicePlan,
        IReadOnlyList<ActualFilterRule> snapshot)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(snapshot);
        return OnboardingBootstrapWritePlanner.Plan(devicePlan, snapshot);
    }
}

/// <summary>
/// Restricted bootstrap writer. Implementations must allowlist paths at compile time,
/// read back after every write, and must not expose a free-form command method.
/// </summary>
public interface IOnboardingBootstrapWritePort
{
    Task<OnboardingBootstrapWriteExecutionResult> ApplyAsync(
        OnboardingBootstrapWrite write,
        IReadOnlyList<ActualFilterRule> liveSnapshot,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of one allowlisted write plus actual-state read-back.</summary>
public sealed class OnboardingBootstrapWriteExecutionResult
{
    public required bool Succeeded { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> SentAttributes { get; init; }

    public required IReadOnlyDictionary<string, string> ReadBack { get; init; }

    public string? Error { get; init; }
}
