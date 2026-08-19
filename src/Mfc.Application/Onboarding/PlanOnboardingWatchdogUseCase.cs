using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Application.Onboarding;

/// <summary>
/// Plans scheduler proof and onboarding watchdog resources (M5-06).
/// Execution is <see cref="IOnboardingWatchdogPort"/>; this use case never talks to RouterOS.
/// </summary>
public static class PlanOnboardingWatchdogUseCase
{
    public static OnboardingWatchdogPlanResult PlanProof(DeviceId deviceId, OnboardingSystemNameFacts names)
        => OnboardingWatchdogPlanner.PlanProof(deviceId, names);

    public static OnboardingWatchdogPlanResult PlanWatchdog(
        OnboardingOperationId operationId,
        DeviceOnboardingPlan devicePlan,
        OnboardingSystemNameFacts names)
        => OnboardingWatchdogPlanner.PlanWatchdog(operationId, devicePlan, names);
}

/// <summary>
/// Restricted scheduler-proof and watchdog writer. Implementations must allowlist Spec §27.2
/// paths at compile time, read back after writes, and must not expose a free-form command method.
/// </summary>
public interface IOnboardingWatchdogPort
{
    Task<OnboardingWatchdogExecutionResult> ProveSchedulerAsync(
        SchedulerProofPlan plan,
        DateTimeOffset routerClock,
        CancellationToken cancellationToken = default);

    Task<OnboardingWatchdogExecutionResult> ArmWatchdogAsync(
        OnboardingWatchdogBundle bundle,
        DateTimeOffset routerClock,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of scheduler proof or watchdog arm plus actual-state read-back.</summary>
public sealed class OnboardingWatchdogExecutionResult
{
    public required bool Succeeded { get; init; }

    public required string Code { get; init; }

    public required IReadOnlyList<string> Paths { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> SentAttributes { get; init; }

    public Hash256? ObservedSourceHash { get; init; }

    public int? RunCount { get; init; }

    public string? Error { get; init; }
}
