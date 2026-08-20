namespace Mfc.Domain.Workflow;

/// <summary>
/// Derived Node workflow status (E2E Workflow Spec §7). Never persisted as an authoritative Node field.
/// </summary>
public enum NodeWorkflowStatus : byte
{
    InventoryIncomplete = 0,
    ConnectionInvalid = 1,
    CaptureRequired = 2,
    TopologyBlocked = 3,
    OnboardingRequired = 4,
    OnboardingInProgress = 5,
    PolicyRequired = 6,
    AnalysisRequired = 7,
    AnalysisBlocked = 8,
    PendingDeployment = 9,
    DeploymentInProgress = 10,
    Synchronized = 11,
    Drifted = 12,
    RecoveryRequired = 13,
}
