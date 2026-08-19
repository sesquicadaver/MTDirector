using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Exact before/after position Desktop binds to (M5-04 AC#10). Does not store RouterOS <c>.id</c>.
/// </summary>
public sealed class AnchorPlacementPreview
{
    public required FilterBuiltInContext Chain { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required AnchorPlacementMode Mode { get; init; }

    public required uint ExpectedAnchorOrdinal { get; init; }

    /// <summary>Human-readable predecessor (empty means start of chain).</summary>
    public required string BeforeLabel { get; init; }

    /// <summary>Human-readable successor (empty means end of chain / APPEND).</summary>
    public required string AfterLabel { get; init; }

    public Hash256? PredecessorFingerprint { get; init; }

    public Hash256? SuccessorFingerprint { get; init; }
}

/// <summary>One placement-planning finding (Onboarding Spec §58).</summary>
public sealed class AnchorPlacementFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Target { get; init; }
}

/// <summary>Outcome of <see cref="AnchorPlacementPlanner"/>.</summary>
public sealed class AnchorPlacementPlanResult
{
    public required IReadOnlyList<AnchorPlacementFinding> Findings { get; init; }

    public AnchorPlacement? Placement { get; init; }

    public AnchorPlacementPreview? Preview { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != OnboardingCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == OnboardingCodes.SeverityBlocker);
}
