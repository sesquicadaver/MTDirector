using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Abstractions.Onboarding;

/// <summary>
/// Default runtime when no live RouterOS onboarding adapter is registered (same pattern as snapshot capture).
/// </summary>
public sealed class NotConfiguredOnboardingRuntime : IOnboardingRuntime
{
    public const string NotConfiguredMessage =
        "Onboarding runtime is not_configured for live RouterOS mutation; inject an adapter for Start/Rollback.";

    public Task<OnboardingExecutionResult> ExecuteAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        DateTimeOffset routerClock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }

    public Task<OnboardingRollbackResult> RollbackAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }

    public Task<OnboardingRecoveryResult> RecoverAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }
}
