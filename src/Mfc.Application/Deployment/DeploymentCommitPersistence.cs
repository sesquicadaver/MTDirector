using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;

namespace Mfc.Application.Deployment;

/// <summary>
/// Persists commit evidence and write-ahead journal after a successful deployment Execute (AUDIT-COMMIT-01).
/// </summary>
public static class DeploymentCommitPersistence
{
    public static async Task PersistAsync(
        IDeploymentStore deployments,
        IDeviceHashStateStore hashStates,
        DeploymentPlan plan,
        DeploymentWorkflowExecutionResult executed,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(executed);

        if (executed.DeviceState is { } deviceState)
        {
            IReadOnlyList<DeviceDeployment> existing = await deployments
                .ListDeviceStatesAsync(deviceState.OperationId, cancellationToken)
                .ConfigureAwait(false);
            if (existing.Any(d => d.DeviceId.Value == deviceState.DeviceId.Value))
            {
                await deployments.SaveDeviceStateAsync(deviceState, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await deployments.AddDeviceStateAsync(deviceState, cancellationToken).ConfigureAwait(false);
            }
        }

        int nextSequence = 1;
        IReadOnlyList<DeploymentStep> prior = executed.DeviceState is null
            ? []
            : await deployments.ListStepsAsync(executed.DeviceState.OperationId, cancellationToken)
                .ConfigureAwait(false);
        if (prior.Count > 0)
        {
            nextSequence = prior.Max(static s => s.Sequence) + 1;
            foreach (DeploymentStep precheck in prior.Where(static s =>
                         s.Kind == DeploymentStepKind.Precheck && s.State == DeploymentStepState.IntentRecorded))
            {
                if (executed.Succeeded && executed.State is DeploymentOperationState.Committed
                    or DeploymentOperationState.NoChanges)
                {
                    precheck.RecordEffectSent(nowUtc);
                    precheck.MarkVerified(nowUtc);
                    await deployments.SaveStepAsync(precheck, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        foreach (AnchorActivationJournalEntry entry in executed.ActivationJournal)
        {
            if (executed.DeviceState is null)
            {
                break;
            }

            DeploymentStep step = DeploymentStep.Create(
                executed.DeviceState.OperationId,
                executed.DeviceState.DeviceId,
                nextSequence++,
                DeploymentStepKind.ActivateAnchor,
                entry.ExpectedBeforeHash,
                entry.DesiredAfterHash,
                nowUtc);
            if (entry.State == DeploymentStepState.Verified)
            {
                step.RecordEffectSent(nowUtc);
                step.MarkVerified(nowUtc);
            }
            else if (entry.State == DeploymentStepState.EffectSent)
            {
                step.RecordEffectSent(nowUtc);
            }
            else if (entry.State == DeploymentStepState.Failed)
            {
                step.MarkFailed(nowUtc, entry.Code);
            }

            await deployments.AddStepAsync(step, cancellationToken).ConfigureAwait(false);
        }

        List<DeploymentCommitSnapshot> snapshots = [];
        if (executed.CommitSnapshot is not null)
        {
            snapshots.Add(executed.CommitSnapshot);
        }

        snapshots.AddRange(executed.MemberCommitSnapshots);
        foreach (DeploymentCommitSnapshot snap in snapshots)
        {
            DeviceDeploymentPlan? devicePlan = plan.DevicePlans
                .FirstOrDefault(p => p.NewArtifactHash.Equals(snap.NewArtifactHash));
            DeviceId deviceId = devicePlan?.DeviceId
                ?? executed.DeviceState?.DeviceId
                ?? plan.DevicePlans[0].DeviceId;

            DeviceHashState? existingHash = await hashStates.GetAsync(deviceId, cancellationToken)
                .ConfigureAwait(false);
            DeviceHashState next = existingHash is null
                ? DeviceHashState.Create(
                    deviceId,
                    desiredPolicyHash: plan.LogicalPolicyHash,
                    desiredArtifactHash: snap.NewArtifactHash,
                    lastCommittedPolicyHash: plan.LogicalPolicyHash,
                    lastCommittedArtifactHash: snap.NewArtifactHash,
                    actualManagedResourceHash: snap.NewArtifactHash,
                    actualKnown: true,
                    anchorKnown: true,
                    updatedAtUtc: snap.CommittedAtUtc)
                : existingHash.With(
                    desiredPolicyHash: plan.LogicalPolicyHash,
                    desiredArtifactHash: snap.NewArtifactHash,
                    lastCommittedPolicyHash: plan.LogicalPolicyHash,
                    lastCommittedArtifactHash: snap.NewArtifactHash,
                    actualManagedResourceHash: snap.NewArtifactHash,
                    actualKnown: true,
                    anchorKnown: true,
                    updatedAtUtc: snap.CommittedAtUtc);
            await hashStates.UpsertAsync(next, cancellationToken).ConfigureAwait(false);

            DeploymentStep commitStep = DeploymentStep.Create(
                snap.OperationId,
                deviceId,
                nextSequence++,
                DeploymentStepKind.Commit,
                snap.OldArtifactHash,
                snap.NewArtifactHash,
                snap.CommittedAtUtc);
            commitStep.RecordEffectSent(snap.CommittedAtUtc);
            commitStep.MarkVerified(snap.CommittedAtUtc);
            await deployments.AddStepAsync(commitStep, cancellationToken).ConfigureAwait(false);
        }
    }
}
