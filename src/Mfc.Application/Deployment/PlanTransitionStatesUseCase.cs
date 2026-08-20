using Mfc.Domain.Deployment;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Deployment;

/// <summary>
/// Plans transition-state validation and activation order (M4-06).
/// Does not talk to RouterOS; activation is <see cref="ActivateAnchorsUseCase"/>.
/// </summary>
public static class PlanTransitionStatesUseCase
{
    public static TransitionStateValidationResult ValidateTransitions(
        IReadOnlyList<AnchorKey> activationOrder,
        IReadOnlyList<AnchorTarget> oldTargets,
        IReadOnlyList<AnchorTarget> newTargets,
        IReadOnlyList<TransitionStateEvidence> evidence,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality = null)
        => TransitionStateValidator.Validate(activationOrder, oldTargets, newTargets, evidence, criticality);

    public static IReadOnlyList<AnchorKey> PlanActivationOrder(
        IEnumerable<AnchorKey> keys,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality = null)
        => DeploymentAnchorOrder.Sort(keys, criticality);
}
