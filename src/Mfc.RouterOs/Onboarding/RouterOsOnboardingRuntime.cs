using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Production <see cref="IOnboardingRuntime"/> over closed onboarding writers (P2-07 / #293).
/// DI registration is gated by write-path enablement in P2-10.
/// </summary>
public sealed class RouterOsOnboardingRuntime : IOnboardingRuntime
{
    private readonly IRouterOsOnboardingSessionFactory _sessions;

    public RouterOsOnboardingRuntime(IRouterOsOnboardingSessionFactory sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = sessions;
    }

    public async Task<OnboardingExecutionResult> ExecuteAsync(
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

        await using RouterOsOnboardingScopedSessions scope = await _sessions
            .OpenAsync(node, plan, cancellationToken)
            .ConfigureAwait(false);
        return await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            scope.Sessions,
            nowUtc,
            routerClock,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OnboardingRollbackResult> RollbackAsync(
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

        await using RouterOsOnboardingScopedSessions scope = await _sessions
            .OpenAsync(node, plan, cancellationToken)
            .ConfigureAwait(false);
        return await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            scope.Sessions,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OnboardingRecoveryResult> RecoverAsync(
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

        await using RouterOsOnboardingScopedSessions scope = await _sessions
            .OpenAsync(node, plan, cancellationToken)
            .ConfigureAwait(false);
        return await RecoverOnboardingUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            scope.Sessions,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
    }
}
