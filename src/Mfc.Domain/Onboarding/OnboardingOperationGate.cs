using Mfc.Domain.Inventory;

namespace Mfc.Domain.Onboarding;

/// <summary>Pure gate for starting/continuing onboarding (Issue Set M5-01 AC#6/#10).</summary>
public static class OnboardingOperationGate
{
    /// <summary>
    /// Ensures a new operation may start: plan current, hash verifies, Node UNMANAGED,
    /// no other nonterminal for the Node.
    /// </summary>
    public static OnboardingGateEvaluation EvaluateCreate(
        Node node,
        OnboardingPlan plan,
        IReadOnlyList<OnboardingOperation> existingForNode,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        if (node.Id != plan.NodeId)
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.DevicePlanCardinality,
                "plan.node_id must match the target Node.");
        }

        if (node.ManagementState != ManagementState.Unmanaged)
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.NodeNotUnmanaged,
                "Node must be UNMANAGED to start onboarding.");
        }

        return EvaluateCreate(plan, existingForNode, nowUtc);
    }

    /// <summary>Ensures a new operation may start: plan current, hash verifies, no other nonterminal for the Node.</summary>
    public static OnboardingGateEvaluation EvaluateCreate(
        OnboardingPlan plan,
        IReadOnlyList<OnboardingOperation> existingForNode,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(existingForNode);
        if (!OnboardingPlanHasher.Compute(plan).Equals(plan.PlanHash))
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.PlanHashMismatch,
                "plan_hash does not match plan content.");
        }

        if (plan.IsExpired(nowUtc))
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.PlanExpired,
                "plan lifetime elapsed.");
        }

        if (existingForNode.Any(o => o.NodeId == plan.NodeId && o.IsNonterminal))
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.NonterminalExists,
                "only one nonterminal onboarding per Node.");
        }

        return OnboardingGateEvaluation.Allow();
    }

    public static OnboardingGateEvaluation EvaluateTransition(
        OnboardingOperation operation,
        OnboardingOperationState next)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.IsTerminal)
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.InvalidTransition,
                "Terminal operations are immutable.");
        }

        if (!OnboardingOperation.CanTransition(operation.State, next))
        {
            return OnboardingGateEvaluation.Reject(
                OnboardingCodes.InvalidTransition,
                $"'{operation.State}' → '{next}' is not allowed.");
        }

        return OnboardingGateEvaluation.Allow();
    }

    /// <summary>Throws when <see cref="EvaluateCreate(OnboardingPlan, IReadOnlyList{OnboardingOperation}, DateTimeOffset)"/> rejects.</summary>
    public static void EnsureCanStart(
        OnboardingPlan plan,
        IReadOnlyList<OnboardingOperation> existingForNode,
        DateTimeOffset nowUtc)
        => ThrowIfRejected(EvaluateCreate(plan, existingForNode, nowUtc));

    /// <summary>Throws when <see cref="EvaluateCreate(Node, OnboardingPlan, IReadOnlyList{OnboardingOperation}, DateTimeOffset)"/> rejects.</summary>
    public static void EnsureCanStart(
        Node node,
        OnboardingPlan plan,
        IReadOnlyList<OnboardingOperation> existingForNode,
        DateTimeOffset nowUtc)
        => ThrowIfRejected(EvaluateCreate(node, plan, existingForNode, nowUtc));

    private static void ThrowIfRejected(OnboardingGateEvaluation evaluation)
    {
        if (!evaluation.Allowed)
        {
            throw new DomainInvariantException($"{evaluation.ErrorCode}: {evaluation.ErrorMessage}");
        }
    }
}
