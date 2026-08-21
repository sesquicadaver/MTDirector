namespace Mfc.Domain.Drift;

/// <summary>
/// next-1 / N1-07: VETH / VLAN / bridge membership / VRF / NAT exposure / hardware-path
/// configuration drift voids static analysis, approval context, compiled artifact readiness,
/// and any unexecuted deployment plan. Observation-only path-class fields do not.
/// </summary>
public static class PathClassConfigDriftVoiding
{
    /// <summary>True when <paramref name="kind"/> is a path-class configuration change (Critical).</summary>
    public static bool IsPathClassConfigurationKind(DriftFindingKind kind)
        => kind is DriftFindingKind.VethConfigChanged
            or DriftFindingKind.VlanConfigChanged
            or DriftFindingKind.BridgeMembershipConfigChanged
            or DriftFindingKind.VrfAssignmentConfigChanged
            or DriftFindingKind.ContainerNatExposureConfigChanged
            or DriftFindingKind.HardwarePathConfigChanged;

    /// <summary>True when <paramref name="kind"/> is a path-class running-state observation.</summary>
    public static bool IsPathClassObservationKind(DriftFindingKind kind)
        => kind is DriftFindingKind.ContainerRunningStateChanged
            or DriftFindingKind.VethRunningStateChanged
            or DriftFindingKind.BridgePortStateChanged
            or DriftFindingKind.HardwareOffloadStateChanged
            or DriftFindingKind.ActiveWanChanged
            or DriftFindingKind.InterfaceRunningStateChanged;

    /// <summary>
    /// Derives voiding flags from a completed <see cref="DriftEvaluation"/>.
    /// Critical path-class findings that block deployment void all readiness surfaces.
    /// </summary>
    public static PathClassVoidingResult Evaluate(DriftEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        bool hasPathClassCritical = evaluation.Findings.Any(static f => IsPathClassConfigurationKind(f.Kind));
        bool voids = hasPathClassCritical && evaluation.BlocksDeployment;
        return new PathClassVoidingResult(
            voidsStaticAnalysis: voids,
            voidsApprovalContext: voids,
            voidsCompiledArtifactReadiness: voids,
            voidsUnexecutedDeploymentPlan: voids);
    }
}

/// <summary>Explicit voiding surface for path-class Critical configuration drift (next-1).</summary>
public sealed class PathClassVoidingResult
{
    public PathClassVoidingResult(
        bool voidsStaticAnalysis,
        bool voidsApprovalContext,
        bool voidsCompiledArtifactReadiness,
        bool voidsUnexecutedDeploymentPlan)
    {
        VoidsStaticAnalysis = voidsStaticAnalysis;
        VoidsApprovalContext = voidsApprovalContext;
        VoidsCompiledArtifactReadiness = voidsCompiledArtifactReadiness;
        VoidsUnexecutedDeploymentPlan = voidsUnexecutedDeploymentPlan;
    }

    public bool VoidsStaticAnalysis { get; }

    public bool VoidsApprovalContext { get; }

    public bool VoidsCompiledArtifactReadiness { get; }

    public bool VoidsUnexecutedDeploymentPlan { get; }

    /// <summary>True when every normative readiness surface is voided.</summary>
    public bool VoidsAll
        => VoidsStaticAnalysis
           && VoidsApprovalContext
           && VoidsCompiledArtifactReadiness
           && VoidsUnexecutedDeploymentPlan;
}
