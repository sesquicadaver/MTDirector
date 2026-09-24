using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-401: AUDIT-COMMIT-01 — commit snapshot and activation journal persist
/// into DeviceHashState + deployment steps (not DTO-only).
/// </summary>
public sealed class AuditCommit01PersistW7401LivingSpecTests
{
    [Fact]
    public async Task Ac1PersistUpdatesLastCommittedArtifactHashAndCommitStep()
    {
        FakeDeploymentStore deployments = new();
        FakeDeviceHashStateStore hashStates = new();
        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        Node node = DeploymentTestFactory.RouterWithDevice(out Device device);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, created: now);
        await deployments.AddPlanAsync(plan);
        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        DeploymentOperationId operationId = DeploymentOperationId.New();
        DeviceDeployment deviceState = DeviceDeployment.Create(operationId, devicePlan.DeviceId, now);
        deviceState.EnsureTransition(DeviceDeploymentState.Prechecked, now);
        deviceState.EnsureTransition(DeviceDeploymentState.Staging, now);
        deviceState.EnsureTransition(DeviceDeploymentState.Staged, now);
        deviceState.EnsureTransition(DeviceDeploymentState.WatchdogArmed, now);
        deviceState.EnsureTransition(DeviceDeploymentState.Activating, now);
        deviceState.EnsureTransition(DeviceDeploymentState.ActiveUnverified, now);
        deviceState.EnsureTransition(DeviceDeploymentState.Verified, now);
        deviceState.EnsureTransition(DeviceDeploymentState.WatchdogDisarmed, now);
        deviceState.EnsureTransition(DeviceDeploymentState.Committed, now);

        DeploymentStep precheck = DeploymentStep.Create(
            operationId,
            devicePlan.DeviceId,
            sequence: 1,
            DeploymentStepKind.Precheck,
            devicePlan.OldArtifactHash,
            devicePlan.NewArtifactHash,
            now);
        await deployments.AddStepAsync(precheck);

        AnchorKey anchorKey = devicePlan.AnchorActivationOrder[0];
        DeploymentCommitSnapshot snapshot = new()
        {
            OperationId = operationId,
            PlanHash = plan.PlanHash,
            NewArtifactHash = devicePlan.NewArtifactHash,
            OldArtifactHash = devicePlan.OldArtifactHash,
            CommittedAtUtc = now,
        };
        DeploymentWorkflowExecutionResult executed = new()
        {
            Succeeded = true,
            State = DeploymentOperationState.Committed,
            Timeline = ["commit"],
            ActivationStarted = true,
            CommitSnapshot = snapshot,
            DeviceState = deviceState,
            ActivationJournal =
            [
                new AnchorActivationJournalEntry
                {
                    Key = anchorKey,
                    State = DeploymentStepState.Verified,
                    ObservedBefore = "old",
                    ObservedAfter = "new",
                    ExpectedBeforeHash = devicePlan.OldArtifactHash,
                    DesiredAfterHash = devicePlan.NewArtifactHash,
                },
            ],
        };

        await DeploymentCommitPersistence.PersistAsync(
                deployments, hashStates, plan, executed, now)
            ;

        DeviceHashState? hash = await hashStates.GetAsync(devicePlan.DeviceId);
        Assert.NotNull(hash);
        Assert.True(devicePlan.NewArtifactHash.Equals(hash!.LastCommittedArtifactHash));
        Assert.True(plan.LogicalPolicyHash.Equals(hash.LastCommittedPolicyHash));

        IReadOnlyList<DeploymentStep> steps = await deployments.ListStepsAsync(operationId);
        Assert.Contains(steps, s => s.Kind == DeploymentStepKind.ActivateAnchor && s.State == DeploymentStepState.Verified);
        Assert.Contains(steps, s => s.Kind == DeploymentStepKind.Commit && s.State == DeploymentStepState.Verified);
        Assert.Contains(steps, s => s.Kind == DeploymentStepKind.Precheck && s.State == DeploymentStepState.Verified);

        IReadOnlyList<DeviceDeployment> devices = await deployments.ListDeviceStatesAsync(operationId);
        Assert.Single(devices);
        Assert.Equal(DeviceDeploymentState.Committed, devices[0].State);
        Assert.Equal(device.Id, devicePlan.DeviceId);
    }

    [Fact]
    public void Ac2RuntimeAndStartWireCommitEvidenceAndQueueLock()
    {
        string root = RepoRoot();
        string runtime = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs"));
        string start = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs"));
        string persist = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/DeploymentCommitPersistence.cs"));
        string standalone = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs"));
        string activate = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/ActivateAnchorsUseCase.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("CommitSnapshot = standalone.CommitSnapshot", runtime, StringComparison.Ordinal);
        Assert.Contains("MemberCommitSnapshots = result.MemberCommitSnapshots", runtime, StringComparison.Ordinal);
        Assert.Contains("DeploymentCommitPersistence.PersistAsync", start, StringComparison.Ordinal);
        Assert.Contains("IDeviceHashStateStore", start, StringComparison.Ordinal);
        Assert.Contains("lastCommittedArtifactHash", persist, StringComparison.Ordinal);
        Assert.Contains("ActivationJournal", standalone, StringComparison.Ordinal);
        Assert.Contains("persistIntentAsync", activate, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-401 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-COMMIT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditCommit01PersistW7401LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-402", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-EVID-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-401 | [#1206](https://github.com/sesquicadaver/MTDirector/issues/1206) | AUDIT-COMMIT-01 — Commit snapshot + journal persist | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-402 | [#1208](https://github.com/sesquicadaver/MTDirector/issues/1208) | Seed next after AUDIT-COMMIT-01 → AUDIT-EVID-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-403 | [#1209](https://github.com/sesquicadaver/MTDirector/issues/1209) | AUDIT-EVID-01 — Real safety evidence (no AllSafeEvidence) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-COMMIT-01 W7-401 (#1206) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-402 (#1208) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-EVID-01", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-COMMIT-01 W7-401 (#1206) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditCommit01PersistW7401", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7402", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-401` | #1206 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-402` | #1208 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-403` | #1209 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", issues, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
