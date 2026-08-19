using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Abstractions.Onboarding;

/// <summary>
/// Device-session runtime for onboarding execute/rollback/recover.
/// Application never talks to RouterOS; Controller injects a RouterOS-backed adapter.
/// </summary>
public interface IOnboardingRuntime
{
    Task<OnboardingExecutionResult> ExecuteAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        DateTimeOffset routerClock,
        CancellationToken cancellationToken = default);

    Task<OnboardingRollbackResult> RollbackAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<OnboardingRecoveryResult> RecoverAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
