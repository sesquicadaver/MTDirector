using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Append-only onboarding plans, mutable operations, and write-ahead steps (M5-01).</summary>
public interface IOnboardingStore
{
    Task AddPlanAsync(OnboardingPlan plan, CancellationToken cancellationToken = default);

    Task<OnboardingPlan?> GetPlanAsync(OnboardingPlanId id, CancellationToken cancellationToken = default);

    Task AddOperationAsync(OnboardingOperation operation, CancellationToken cancellationToken = default);

    Task SaveOperationAsync(OnboardingOperation operation, CancellationToken cancellationToken = default);

    Task<OnboardingOperation?> GetOperationAsync(
        OnboardingOperationId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnboardingOperation>> ListNonterminalByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>Bounded global scan of nonterminal onboarding operations (M6-03 recovery job).</summary>
    Task<IReadOnlyList<OnboardingOperation>> ListNonterminalAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task AddStepAsync(OnboardingStep onboardingStep, CancellationToken cancellationToken = default);

    Task SaveStepAsync(OnboardingStep onboardingStep, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnboardingStep>> ListStepsAsync(
        OnboardingOperationId operationId,
        CancellationToken cancellationToken = default);
}
