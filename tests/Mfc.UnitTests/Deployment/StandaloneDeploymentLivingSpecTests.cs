using System.Globalization;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-08 AC 1–10 (Safe Deployment Spec §35).
/// </summary>
public sealed class StandaloneDeploymentLivingSpecTests
{
    private const string ArtifactId = "0123456789abcdef";

    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ac1PreconditionsAreRechecked()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, SeedChannel(plan, toNew: false)),
            existingForNode: [],
            packetPathPairs: DeploymentTestFactory.CpuPairs(),
            addressLists: [],
            chains: [],
            observedResourceHashAfterStaging: plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.Contains(result.Timeline, static t => t == "precheck:revalidated");
    }

    [Fact]
    public async Task Ac2NoChangesPerformsNoWrites()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0, noChanges: true);
        Assert.True(StandaloneDeploymentPolicy.IsNoChanges(plan.DevicePlans[0]));
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.NoChanges, result.State);
        Assert.False(result.WroteToDevice);
        Assert.DoesNotContain(channel.Sent, static s => DeploymentWritePaths.IsFilterSet(s.Path));
        Assert.DoesNotContain(channel.Sent, static s => s.Path == DeploymentWritePath.SystemScriptAdd);
    }

    [Fact]
    public async Task Ac3StagingDoesNotCutOverActiveTraffic()
    {
        (StandaloneDeploymentResult result, RecordingChannel channel, _) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t == "stage:detached-only");
        int firstSet = channel.Sent.FindIndex(static s => DeploymentWritePaths.IsFilterSet(s.Path));
        int firstScript = channel.Sent.FindIndex(static s => s.Path == DeploymentWritePath.SystemScriptAdd);
        Assert.True(firstScript >= 0);
        Assert.True(firstSet > firstScript, "anchor set must happen after watchdog arm, not during staging");
    }

    [Fact]
    public async Task Ac4WatchdogIsArmedBeforeActivation()
    {
        (StandaloneDeploymentResult result, _, _) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(result.WatchdogArmedBeforeActivation);
        int arm = result.Timeline.ToList().FindIndex(static t => t == "watchdog:armed");
        int act = result.Timeline.ToList().FindIndex(static t => t == "activate:done");
        Assert.True(arm >= 0 && act > arm);
    }

    [Fact]
    public async Task Ac5FailedVerificationTriggersRollback()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            observedResourceHashAfterStaging: DeploymentTestFactory.H("wrong-hash"),
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentOperationState.RolledBack, result.State);
        Assert.Contains(result.Timeline, static t => t.StartsWith("rollback-anchor:", StringComparison.Ordinal));
        Assert.Contains(result.Timeline, static t => t == "rolled-back");
        Assert.True(result.DetachedArtifactPreservedOnFailure);
    }

    [Fact]
    public async Task Ac6WatchdogDisabledBeforeDurableCommit()
    {
        (StandaloneDeploymentResult result, _, _) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(result.WatchdogDisarmedBeforeCommit);
        int disarm = result.Timeline.ToList().FindIndex(static t => t == "watchdog:disarmed");
        int commit = result.Timeline.ToList().FindIndex(static t => t.StartsWith("commit:", StringComparison.Ordinal));
        Assert.True(disarm >= 0 && commit > disarm);
        Assert.Equal(DeploymentOperationState.Committed, result.State);
    }

    [Fact]
    public async Task Ac7OldArtifactRemainsForRollback()
    {
        (StandaloneDeploymentResult result, _, DeploymentPlan plan) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.NotNull(result.CommitSnapshot);
        Assert.Equal(plan.DevicePlans[0].OldArtifactHash, result.CommitSnapshot!.OldArtifactHash);
        Assert.NotEqual(result.CommitSnapshot.OldArtifactHash, result.CommitSnapshot.NewArtifactHash);
    }

    [Fact]
    public async Task Ac8NewDetachedArtifactIsNotRemovedOnFailure()
    {
        StandaloneDeploymentResult result = (await Ac5FailedVerificationTriggersRollback_Internal()).Result;
        Assert.False(result.Succeeded);
        Assert.True(result.DetachedArtifactPreservedOnFailure);
        Assert.DoesNotContain(result.Timeline, static t => t.Contains("remove", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ac9CommitSnapshotIsStored()
    {
        (StandaloneDeploymentResult result, _, DeploymentPlan plan) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        DeploymentCommitSnapshot snap = Assert.IsType<DeploymentCommitSnapshot>(result.CommitSnapshot);
        Assert.Equal(plan.PlanHash, snap.PlanHash);
        Assert.Equal(plan.DevicePlans[0].NewArtifactHash, snap.NewArtifactHash);
        Assert.NotEqual(default, snap.CommittedAtUtc);
    }

    [Fact]
    public async Task Ac10RedeploySameArtifactReturnsNoChanges()
    {
        await Ac2NoChangesPerformsNoWrites();
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan again = DeploymentTestFactory.PlanFor(node, T0, noChanges: true);
        Assert.True(StandaloneDeploymentPolicy.IsNoChanges(again.DevicePlans[0]));
    }

    [Fact]
    public void StandaloneRejectsVrrp()
    {
        Node vrrp = DeploymentTestFactory.VrrpWithMembers(out _, out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(vrrp, T0);
        Assert.Throws<DomainInvariantException>(() => StandaloneDeploymentPolicy.EnsureEligible(vrrp, plan));
    }

    [Fact]
    public async Task SwitchNodeHappyPathCommits()
    {
        Node node = DeploymentTestFactory.SwitchWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.Committed, result.State);
    }

    [Fact]
    public async Task ExecuteRejectsVrrpViaCatch()
    {
        Node vrrp = DeploymentTestFactory.VrrpWithMembers(out Device first, out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(vrrp, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, first.Id, T0);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            vrrp,
            plan,
            operation,
            device,
            new FakeRuntime(first.Id, new RecordingChannel()),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.StandaloneNodeRequired, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t.StartsWith("fail:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRejectsDeviceMismatch()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(DeviceId.New(), SeedChannel(plan, toNew: false)),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.InvalidTransition, result.ErrorCode);
    }

    [Fact]
    public async Task RecheckFailsOnHardwareOffload()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, SeedChannel(plan, toNew: false)),
            [],
            DeploymentTestFactory.HardwareOffloadedPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Timeline, static t => t.StartsWith("fail:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StagesAddressListThenCommits()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        AddressListArtifactDraft list = DesiredAddressList(IpAddressFamily.IPv4, "10.0.0.0/8", "192.0.2.1");
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [list],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(result.WroteToDevice);
        Assert.Contains(result.Timeline, t => t == $"stage-al:{list.Name}");
    }

    [Fact]
    public async Task ChainStagingCollisionFailsBeforeActivation()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        ChainArtifactDraft chain = DesiredChain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        channel.Seed(
            DeploymentReadSurface.Ipv4Filter,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = "*bad",
                ["chain"] = chain.Name,
                ["action"] = "accept",
                ["comment"] = "foreign",
                ["disabled"] = "no",
            });
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [chain],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, result.ErrorCode);
        Assert.DoesNotContain(result.Timeline, static t => t == "watchdog:armed");
    }

    [Fact]
    public async Task AddressListCollisionFailsBeforeActivation()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        AddressListArtifactDraft list = DesiredAddressList(IpAddressFamily.IPv4, "10.0.0.0/8");
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        SeedAddressList(channel, list, "10.0.0.0/8", "203.0.113.1");
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [list],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, result.ErrorCode);
        Assert.DoesNotContain(result.Timeline, static t => t == "watchdog:armed");
    }

    [Fact]
    public async Task WatchdogNameCollisionBlocksArm()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        string token = DeploymentWatchdogNames.Token(operation.Id, plan.DevicePlans[0].DeviceId);
        string script = DeploymentWatchdogNames.RollbackScript(token);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(
                plan.DevicePlans[0].DeviceId,
                SeedChannel(plan, toNew: false),
                names: new DeploymentSystemNameFacts
                {
                    ScriptNames = [script],
                    SchedulerNames = [],
                }),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorCode);
        Assert.DoesNotContain(result.Timeline, static t => t == "activate:done");
    }

    [Fact]
    public async Task ArmWatchdogFailureFailsOperation()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        ScriptedWatchdog watchdog = new() { ArmSucceeds = false };
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, SeedChannel(plan, toNew: false), watchdog),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.WatchdogArmFailed, result.ErrorCode);
        Assert.False(result.WatchdogArmedBeforeActivation);
    }

    [Fact]
    public async Task ActivateFailureRollsBackAnchors()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        // No permanent anchors seeded → activate precondition fails; rollback restore then needs recovery.
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, new RecordingChannel()),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Timeline, static t => t == "activate:failed");
        Assert.True(
            result.State is DeploymentOperationState.RolledBack or DeploymentOperationState.RecoveryRequired,
            result.State.ToString());
    }

    [Fact]
    public async Task UnknownAnchorTargetRequiresRecovery()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        Dictionary<string, string>? row = channel.FindAnchor(plan.DevicePlans[0].AnchorActivationOrder[0]);
        Assert.NotNull(row);
        row!["jump-target"] = "mfc-unknown-third-target";
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentOperationState.RecoveryRequired, result.State);
        Assert.Contains(result.Timeline, static t => t == "recovery-required");
    }

    [Fact]
    public async Task DisarmFailureTriggersRollback()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        FakeRuntime runtime = new(plan.DevicePlans[0].DeviceId, channel);
        runtime.ReplaceWatchdog(new DisarmFailWatchdog(runtime.Watchdog));
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            runtime,
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Timeline, static t => t == "watchdog:disarm-failed");
        Assert.Equal(DeploymentOperationState.RecoveryRequired, result.State);
    }

    [Fact]
    public async Task RollbackRestoreFailureRequiresRecovery()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        channel.FailFilterSetsAfter = plan.DevicePlans[0].AnchorActivationOrder.Count;
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            observedResourceHashAfterStaging: DeploymentTestFactory.H("wrong-hash"),
            T0.AddMinutes(1),
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentOperationState.RecoveryRequired, result.State);
        Assert.Contains(result.Timeline, static t => t.StartsWith("rollback-anchor-failed:", StringComparison.Ordinal));
    }

    private static async Task<(StandaloneDeploymentResult Result, RecordingChannel Channel, DeploymentPlan Plan)> HappyPathAsync()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        return (result, channel, plan);
    }

    private static async Task<(StandaloneDeploymentResult Result, RecordingChannel Channel)> Ac5FailedVerificationTriggersRollback_Internal()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            DeploymentTestFactory.H("wrong-hash"),
            T0.AddMinutes(1),
            T0);
        return (result, channel);
    }

    private static AddressListArtifactDraft DesiredAddressList(IpAddressFamily family, params string[] addresses)
    {
        AddressListEntryArtifact[] entries = addresses
            .Select(AddressListEntryArtifact.Create)
            .OrderBy(static e => e.Address, StringComparer.Ordinal)
            .ToArray();
        Hash256 hash = RouterOsFilterArtifactIdentity.HashAddressListContent(family, entries);
        string name = ManagedChainNamespace.AddressListName(
            family,
            hash.ToString()[..RouterOsFilterArtifactIdentity.ArtifactIdHexLength]);
        return new AddressListArtifactDraft
        {
            Family = family,
            Name = name,
            Entries = entries,
        };
    }

    private static ChainArtifactDraft DesiredChain(FilterChainArtifactRole role, string comment, string action)
    {
        FilterRuleArtifact rule = FilterRuleArtifact.Create(0, action, comment, structuralRole: "s");
        return new ChainArtifactDraft
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Input,
            Name = ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                role,
                ArtifactId),
            Role = role,
            Rules = [rule],
        };
    }

    private static void SeedAddressList(RecordingChannel channel, AddressListArtifactDraft desired, params string[] addresses)
    {
        DeploymentReadSurface surface = desired.Family == IpAddressFamily.IPv4
            ? DeploymentReadSurface.Ipv4AddressList
            : DeploymentReadSurface.Ipv6AddressList;
        foreach (string address in addresses)
        {
            channel.Seed(
                surface,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*" + address.GetHashCode(StringComparison.Ordinal).ToString(CultureInfo.InvariantCulture),
                    ["list"] = desired.Name,
                    ["address"] = address,
                    ["dynamic"] = "false",
                });
        }
    }

    private static RecordingChannel SeedChannel(DeviceDeploymentPlan plan, bool toNew)
    {
        RecordingChannel channel = new();
        int id = 1;
        IReadOnlyList<AnchorTarget> targets = toNew ? plan.NewAnchorTargets : plan.OldAnchorTargets;
        foreach (AnchorTarget target in targets)
        {
            string chain = target.Key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => "input",
            };
            channel.Seed(
                DeploymentReadSurface.Ipv4Filter,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*" + id.ToString(CultureInfo.InvariantCulture),
                    ["chain"] = chain,
                    ["action"] = "jump",
                    ["jump-target"] = target.JumpTarget,
                    ["comment"] = target.Key.Marker,
                    ["disabled"] = "false",
                });
            id++;
        }

        return channel;
    }

    private static RecordingChannel SeedChannel(DeploymentPlan plan, bool toNew)
        => SeedChannel(plan.DevicePlans[0], toNew);

    private sealed class ScriptedWatchdog : IDeploymentWatchdogPort
    {
        public bool ArmSucceeds { get; init; } = true;

        public Task<DeploymentWatchdogExecutionResult> ArmWatchdogAsync(
            DeploymentWatchdogBundle bundle,
            DateTimeOffset routerClock,
            TimeSpan? remainingTtl = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWatchdogExecutionResult
            {
                Succeeded = ArmSucceeds,
                Code = ArmSucceeds ? "OK" : DeploymentCodes.WatchdogArmFailed,
                Paths = [],
            });

        public Task<DeploymentWatchdogExecutionResult> DisarmWatchdogAsync(
            DeploymentWatchdogBundle bundle,
            TimeSpan? remainingTtl = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWatchdogExecutionResult
            {
                Succeeded = true,
                Code = "OK",
                Paths = [],
            });

        public Task<DeploymentWatchdogExecutionResult> CleanupWatchdogAsync(
            DeploymentOperationId deploymentId,
            DeviceId deviceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWatchdogExecutionResult
            {
                Succeeded = true,
                Code = "OK",
                Paths = [],
            });
    }

    private sealed class DisarmFailWatchdog : IDeploymentWatchdogPort
    {
        private readonly IDeploymentWatchdogPort _inner;

        public DisarmFailWatchdog(IDeploymentWatchdogPort inner) => _inner = inner;

        public Task<DeploymentWatchdogExecutionResult> ArmWatchdogAsync(
            DeploymentWatchdogBundle bundle,
            DateTimeOffset routerClock,
            TimeSpan? remainingTtl = null,
            CancellationToken cancellationToken = default)
            => _inner.ArmWatchdogAsync(bundle, routerClock, remainingTtl, cancellationToken);

        public Task<DeploymentWatchdogExecutionResult> DisarmWatchdogAsync(
            DeploymentWatchdogBundle bundle,
            TimeSpan? remainingTtl = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWatchdogExecutionResult
            {
                Succeeded = false,
                Code = DeploymentCodes.WatchdogDisableFailed,
                Paths = [],
            });

        public Task<DeploymentWatchdogExecutionResult> CleanupWatchdogAsync(
            DeploymentOperationId deploymentId,
            DeviceId deviceId,
            CancellationToken cancellationToken = default)
            => _inner.CleanupWatchdogAsync(deploymentId, deviceId, cancellationToken);
    }

    private sealed class FakeRuntime : IStandaloneDeploymentDeviceRuntime, IAsyncDisposable
    {
        private readonly RecordingChannel _channel;
        private readonly RouterOsDeploymentSession _session;
        private readonly DeploymentSystemNameFacts? _names;

        public FakeRuntime(
            DeviceId deviceId,
            RecordingChannel channel,
            IDeploymentWatchdogPort? watchdog = null,
            DeploymentSystemNameFacts? names = null)
        {
            DeviceId = deviceId;
            _channel = channel;
            _session = new RouterOsDeploymentSession(channel);
            Watchdog = watchdog ?? new DeploymentWatchdogWriter(_session);
            FreshSessions = new FreshFactory(channel);
            _names = names;
        }

        public void ReplaceWatchdog(IDeploymentWatchdogPort watchdog) => Watchdog = watchdog;

        public DeviceId DeviceId { get; }

        public IRouterOsDeploymentSession Session => _session;

        public IDeploymentWatchdogPort Watchdog { get; private set; }

        public IDeploymentFreshSessionFactory FreshSessions { get; }

        public Task<DeploymentSystemNameFacts> ReadSystemNamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_names ?? new DeploymentSystemNameFacts
            {
                ScriptNames = _channel.ScriptNames().ToArray(),
                SchedulerNames = _channel.SchedulerNames().ToArray(),
            });

        public ValueTask DisposeAsync() => _session.DisposeAsync();
    }

    private sealed class FreshFactory : IDeploymentFreshSessionFactory
    {
        private readonly RecordingChannel _channel;

        public FreshFactory(RecordingChannel channel) => _channel = channel;

        public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IRouterOsDeploymentSession>(new RouterOsDeploymentSession(_channel));
    }

    private sealed class RecordingChannel : IDeploymentWriteChannel
    {
        private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();
        private int _nextId = 1;
        private int _filterSetCount;

        public List<(DeploymentWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public int FailFilterSetsAfter { get; set; } = int.MaxValue;

        public void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                list = [];
                _prints[surface] = list;
            }

            list.Add(new Dictionary<string, string>(row, StringComparer.Ordinal));
        }

        public Dictionary<string, string>? FindAnchor(AnchorKey key)
        {
            if (!_prints.TryGetValue(DeploymentReadSurface.Ipv4Filter, out List<Dictionary<string, string>>? list))
            {
                return null;
            }

            string chain = key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => "input",
            };
            return list.FirstOrDefault(r =>
                string.Equals(r.GetValueOrDefault("comment"), key.Marker, StringComparison.Ordinal)
                && string.Equals(r.GetValueOrDefault("chain"), chain, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> ScriptNames()
            => _prints.GetValueOrDefault(DeploymentReadSurface.Script)?.Select(r => r["name"]) ?? [];

        public IEnumerable<string> SchedulerNames()
            => _prints.GetValueOrDefault(DeploymentReadSurface.Scheduler)?.Select(r => r["name"]) ?? [];

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            DeploymentWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            string fixedPath = DeploymentWritePaths.Fixed(path);
            if (fixedPath.EndsWith("/add", StringComparison.Ordinal))
            {
                DeploymentReadSurface surface = path switch
                {
                    DeploymentWritePath.SystemScriptAdd => DeploymentReadSurface.Script,
                    DeploymentWritePath.SystemSchedulerAdd => DeploymentReadSurface.Scheduler,
                    DeploymentWritePath.Ipv4FilterAdd => DeploymentReadSurface.Ipv4Filter,
                    DeploymentWritePath.Ipv6FilterAdd => DeploymentReadSurface.Ipv6Filter,
                    DeploymentWritePath.Ipv4AddressListAdd => DeploymentReadSurface.Ipv4AddressList,
                    DeploymentWritePath.Ipv6AddressListAdd => DeploymentReadSurface.Ipv6AddressList,
                    _ => throw new InvalidOperationException(path.ToString()),
                };
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = "*" + _nextId.ToString(CultureInfo.InvariantCulture);
                _nextId++;
                Seed(surface, row);
            }
            else if (DeploymentWritePaths.IsFilterSet(path) || path == DeploymentWritePath.SystemSchedulerSet)
            {
                if (DeploymentWritePaths.IsFilterSet(path))
                {
                    _filterSetCount++;
                    if (_filterSetCount > FailFilterSetsAfter)
                    {
                        // Leave jump-target unchanged so read-back fails (rollback restore failure).
                        return Task.FromResult<IReadOnlyDictionary<string, string>>(
                            new Dictionary<string, string>(StringComparer.Ordinal));
                    }
                }

                string id = attributes.Single(static a => a.Key == ".id").Value;
                DeploymentReadSurface surface = path == DeploymentWritePath.SystemSchedulerSet
                    ? DeploymentReadSurface.Scheduler
                    : DeploymentReadSurface.Ipv4Filter;
                Dictionary<string, string> row = _prints[surface].Single(r => r[".id"] == id);
                foreach ((string key, string value) in attributes.Where(static a => a.Key != ".id"))
                {
                    row[key] = value;
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            DeploymentReadSurface surface,
            CancellationToken cancellationToken = default)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
            }

            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>(
                list.Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                    .ToArray());
        }

        public Task<ChannelPingResult> PingAsync(
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChannelPingResult { Sent = 3, Received = 3 });
    }
}
