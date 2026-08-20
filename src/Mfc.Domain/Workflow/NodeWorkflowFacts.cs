namespace Mfc.Domain.Workflow;

/// <summary>
/// Immutable inputs for <see cref="NodeWorkflowStatusProjector"/>. Contains facts only — never a stored status.
/// </summary>
public sealed class NodeWorkflowFacts
{
    public bool RecoveryRequired { get; }

    public ActiveEffectfulOperationKind ActiveEffectfulOperation { get; }

    /// <summary>
    /// Readiness blockers already ordered by E2E §7 priority within the readiness band
    /// (InventoryIncomplete … AnalysisBlocked). Empty when none apply.
    /// </summary>
    public IReadOnlyList<NodeWorkflowStatus> ReadinessBlockers { get; }

    /// <summary>Per-device hash states (any order; projector sorts deterministically).</summary>
    public IReadOnlyList<DeviceHashState> DeviceHashStates { get; }

    public NodeWorkflowFacts(
        bool recoveryRequired,
        ActiveEffectfulOperationKind activeEffectfulOperation,
        IReadOnlyList<NodeWorkflowStatus> readinessBlockers,
        IReadOnlyList<DeviceHashState> deviceHashStates)
    {
        ArgumentNullException.ThrowIfNull(readinessBlockers);
        ArgumentNullException.ThrowIfNull(deviceHashStates);
        EnsureReadinessOnly(readinessBlockers);
        RecoveryRequired = recoveryRequired;
        ActiveEffectfulOperation = activeEffectfulOperation;
        ReadinessBlockers = readinessBlockers;
        DeviceHashStates = deviceHashStates;
    }

    private static void EnsureReadinessOnly(IReadOnlyList<NodeWorkflowStatus> blockers)
    {
        foreach (NodeWorkflowStatus status in blockers)
        {
            if (!IsReadinessBlocker(status))
            {
                throw new DomainInvariantException(
                    $"NodeWorkflowFacts readiness blockers must not include '{status}'.");
            }
        }
    }

    /// <summary>True for statuses that live in the readiness-blocker priority band (E2E §7).</summary>
    public static bool IsReadinessBlocker(NodeWorkflowStatus status)
        => status is NodeWorkflowStatus.InventoryIncomplete
            or NodeWorkflowStatus.ConnectionInvalid
            or NodeWorkflowStatus.CaptureRequired
            or NodeWorkflowStatus.TopologyBlocked
            or NodeWorkflowStatus.OnboardingRequired
            or NodeWorkflowStatus.PolicyRequired
            or NodeWorkflowStatus.AnalysisRequired
            or NodeWorkflowStatus.AnalysisBlocked;
}
