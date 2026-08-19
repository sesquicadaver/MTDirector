using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Onboarding;

/// <summary>
/// Plans an explicit permanent-anchor placement from operator intent (M5-04).
/// Read-only: does not add, move, or rewrite RouterOS rules.
/// </summary>
public static class PlanAnchorPlacementUseCase
{
    /// <summary>Validates intent against the current ordered filter snapshot.</summary>
    public static AnchorPlacementPlanResult Execute(
        AnchorPlacementIntent intent,
        IReadOnlyList<ActualFilterRule> snapshot)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(snapshot);
        return AnchorPlacementPlanner.Plan(intent, snapshot);
    }

    /// <summary>Fails closed when filter order or neighbor fingerprints drifted.</summary>
    public static AnchorPlacementPlanResult Revalidate(
        AnchorPlacement placement,
        IReadOnlyList<ActualFilterRule> snapshot)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(snapshot);
        return AnchorPlacementPlanner.Revalidate(placement, snapshot);
    }
}
