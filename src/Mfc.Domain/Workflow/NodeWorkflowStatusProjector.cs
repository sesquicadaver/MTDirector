namespace Mfc.Domain.Workflow;

/// <summary>
/// Pure projector applying E2E Spec §7 priority over <see cref="NodeWorkflowFacts"/>.
/// VRRP: node status is the highest-priority contribution while every device projection is retained.
/// </summary>
public static class NodeWorkflowStatusProjector
{
    /// <summary>Projects facts into a deterministic <see cref="NodeWorkflowProjection"/>.</summary>
    public static NodeWorkflowProjection Project(NodeWorkflowFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        DeviceWorkflowProjection[] devices = facts.DeviceHashStates
            .OrderBy(static s => s.DeviceId.Value)
            .Select(static state =>
            {
                DeviceSyncClassification classification = DeviceHashStateClassifier.Classify(state);
                return new DeviceWorkflowProjection
                {
                    DeviceId = state.DeviceId,
                    HashState = state,
                    SyncClassification = classification,
                    ContributingStatus = ToContributingStatus(classification),
                };
            })
            .ToArray();

        NodeWorkflowStatus nodeStatus = SelectNodeStatus(facts, devices);
        return new NodeWorkflowProjection
        {
            NodeStatus = nodeStatus,
            Devices = devices,
        };
    }

    private static NodeWorkflowStatus SelectNodeStatus(
        NodeWorkflowFacts facts,
        IReadOnlyList<DeviceWorkflowProjection> devices)
    {
        // Priority: RECOVERY_REQUIRED > active effectful op > DRIFTED > readiness > PENDING > SYNCHRONIZED
        if (facts.RecoveryRequired
            || devices.Any(static d => d.SyncClassification == DeviceSyncClassification.RecoveryRequired))
        {
            return NodeWorkflowStatus.RecoveryRequired;
        }

        if (facts.ActiveEffectfulOperation == ActiveEffectfulOperationKind.Onboarding)
        {
            return NodeWorkflowStatus.OnboardingInProgress;
        }

        if (facts.ActiveEffectfulOperation == ActiveEffectfulOperationKind.Deployment)
        {
            return NodeWorkflowStatus.DeploymentInProgress;
        }

        if (devices.Any(static d => d.SyncClassification == DeviceSyncClassification.Drifted))
        {
            return NodeWorkflowStatus.Drifted;
        }

        if (facts.ReadinessBlockers.Count > 0)
        {
            return SelectHighestPriorityReadiness(facts.ReadinessBlockers);
        }

        if (devices.Any(static d => d.SyncClassification == DeviceSyncClassification.PendingDeployment))
        {
            return NodeWorkflowStatus.PendingDeployment;
        }

        if (devices.Any(static d => d.SyncClassification == DeviceSyncClassification.Synchronized))
        {
            return NodeWorkflowStatus.Synchronized;
        }

        // No sync contribution and no declared readiness facts → inventory incomplete (fail-closed).
        return NodeWorkflowStatus.InventoryIncomplete;
    }

    private static NodeWorkflowStatus SelectHighestPriorityReadiness(IReadOnlyList<NodeWorkflowStatus> blockers)
    {
        NodeWorkflowStatus best = blockers[0];
        int bestRank = ReadinessRank(best);
        for (int i = 1; i < blockers.Count; i++)
        {
            int rank = ReadinessRank(blockers[i]);
            if (rank < bestRank)
            {
                best = blockers[i];
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>Lower rank = higher priority within the readiness band (E2E §7 list order).</summary>
    private static int ReadinessRank(NodeWorkflowStatus status)
        => status switch
        {
            NodeWorkflowStatus.InventoryIncomplete => 0,
            NodeWorkflowStatus.ConnectionInvalid => 1,
            NodeWorkflowStatus.CaptureRequired => 2,
            NodeWorkflowStatus.TopologyBlocked => 3,
            NodeWorkflowStatus.OnboardingRequired => 4,
            NodeWorkflowStatus.PolicyRequired => 5,
            NodeWorkflowStatus.AnalysisRequired => 6,
            NodeWorkflowStatus.AnalysisBlocked => 7,
            _ => throw new DomainInvariantException($"'{status}' is not a readiness blocker."),
        };

    private static NodeWorkflowStatus? ToContributingStatus(DeviceSyncClassification classification)
        => classification switch
        {
            DeviceSyncClassification.RecoveryRequired => NodeWorkflowStatus.RecoveryRequired,
            DeviceSyncClassification.Drifted => NodeWorkflowStatus.Drifted,
            DeviceSyncClassification.PendingDeployment => NodeWorkflowStatus.PendingDeployment,
            DeviceSyncClassification.Synchronized => NodeWorkflowStatus.Synchronized,
            DeviceSyncClassification.Incomplete => null,
            _ => throw new DomainInvariantException($"Unknown sync classification '{classification}'."),
        };
}
