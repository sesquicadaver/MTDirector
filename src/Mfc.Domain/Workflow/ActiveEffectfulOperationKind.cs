namespace Mfc.Domain.Workflow;

/// <summary>Node-level effectful operation currently in progress (E2E Spec §7 priority band).</summary>
public enum ActiveEffectfulOperationKind : byte
{
    None = 0,
    Onboarding = 1,
    Deployment = 2,
}
