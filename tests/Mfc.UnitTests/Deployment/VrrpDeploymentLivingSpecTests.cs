using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-10 AC 1–13 (Safe Deployment Spec §37–§42).
/// </summary>
public sealed class VrrpDeploymentLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ac1AllMembersArePrechecked()
    {
        (VrrpDeploymentResult result, ScriptedCluster cluster) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t == "precheck:all");
        Assert.All(cluster.Members, static m => Assert.True(m.Prechecked));
    }

    [Fact]
    public async Task Ac2AllArtifactsStagedBeforeActivation()
    {
        (VrrpDeploymentResult result, ScriptedCluster cluster) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        int stageAll = result.Timeline.ToList().FindIndex(static t => t == "stage:all");
        int firstActivate = result.Timeline.ToList().FindIndex(static t => t.StartsWith("activate:", StringComparison.Ordinal));
        Assert.True(stageAll >= 0 && firstActivate > stageAll);
        Assert.All(cluster.Members, static m => Assert.True(m.Staged));
    }

    [Fact]
    public async Task Ac3AllWatchdogsArmedBeforeActivation()
    {
        (VrrpDeploymentResult result, _) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        int armed = result.Timeline.ToList().FindIndex(static t => t == "watchdog:all-armed");
        int firstActivate = result.Timeline.ToList().FindIndex(static t => t.StartsWith("activate:", StringComparison.Ordinal));
        Assert.True(armed >= 0 && firstActivate > armed);
    }

    [Fact]
    public void Ac4StandbyOnlyMembersActivateFirst()
    {
        DeviceId standby = DeviceId.New();
        DeviceId master = DeviceId.New();
        VrrpRoleVector vector = new()
        {
            Members =
            [
                Snapshot(standby, VrrpMemberObservedState.Backup, independentTraffic: false),
                Snapshot(master, VrrpMemberObservedState.Master, independentTraffic: false),
            ],
        };
        VrrpActivationPlan plan = VrrpActivationOrderPlanner.Plan(vector);
        Assert.Equal(VrrpDeploymentMemberClass.StandbyOnly, plan.OrderedMembers[0].Class);
        Assert.Equal(standby, plan.OrderedMembers[0].DeviceId);
        Assert.Equal(VrrpDeploymentMemberClass.TrafficBearing, plan.OrderedMembers[1].Class);
    }

    [Fact]
    public void Ac5TrafficBearingMembersActivateLast()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        VrrpRoleVector vector = new()
        {
            Members =
            [
                Snapshot(a, VrrpMemberObservedState.Master, independentTraffic: false),
                Snapshot(b, VrrpMemberObservedState.Backup, independentTraffic: false),
            ],
        };
        VrrpActivationPlan plan = VrrpActivationOrderPlanner.Plan(vector);
        Assert.Equal(VrrpDeploymentMemberClass.TrafficBearing, plan.OrderedMembers[^1].Class);
        Assert.Equal(a, plan.OrderedMembers[^1].DeviceId);
    }

    [Fact]
    public void Ac6UnknownRoleClassifiesAsTrafficBearing()
    {
        VrrpMemberRoleSnapshot unknown = Snapshot(DeviceId.New(), VrrpMemberObservedState.Unknown, independentTraffic: false);
        Assert.Equal(VrrpDeploymentMemberClass.TrafficBearing, VrrpMemberClassifier.Classify(unknown));
    }

    [Fact]
    public async Task Ac7RoleVectorIsReadBeforeEachMember()
    {
        (VrrpDeploymentResult result, ScriptedCluster cluster) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(cluster.Members.Sum(static m => m.RoleReadCount) >= cluster.Members.Length + 2);
        Assert.Contains(result.Timeline, static t => t.StartsWith("role-vector:before:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac8RoleChangeAfterFirstActivationTriggersRollback()
    {
        Node node = DeploymentTestFactory.VrrpWithMembers(out Device first, out Device second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedMember m1 = new(first.Id, VrrpMemberObservedState.Backup);
        ScriptedMember m2 = new(second.Id, VrrpMemberObservedState.Master);
        m2.FlipRoleAfterFirstPeerActivation = true;
        m2.PeerActivatedSignal = () => m1.Activated;
        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [m1, m2],
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.VrrpRoleChangedDuringDeployment, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t == "role-change:detected");
        Assert.NotEmpty(result.RolledBackMembers);
    }

    [Fact]
    public async Task Ac9UnreachableMemberBeforeActivationBlocks()
    {
        Node node = DeploymentTestFactory.VrrpWithMembers(out Device first, out Device second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedMember m1 = new(first.Id, VrrpMemberObservedState.Backup) { Reachable = false };
        ScriptedMember m2 = new(second.Id, VrrpMemberObservedState.Master);
        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [m1, m2],
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.VrrpMemberUnreachable, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.Blocked, result.State);
        Assert.DoesNotContain(result.Timeline, static t => t.StartsWith("activate:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac10PartialActivationRollsBackReachableMembers()
    {
        Node node = DeploymentTestFactory.VrrpWithMembers(out Device first, out Device second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        // Standby activates first; second becomes unreachable after first activation.
        ScriptedMember standby = new(first.Id, VrrpMemberObservedState.Backup);
        ScriptedMember master = new(second.Id, VrrpMemberObservedState.Master)
        {
            BecomeUnreachableAfterPeerActivation = true,
            PeerActivatedSignal = () => standby.Activated,
        };
        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [standby, master],
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Contains(result.RolledBackMembers, id => id.Equals(standby.DeviceId));
        Assert.Contains(result.Timeline, static t => t.StartsWith("rollback:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac11UnreachableMemberKeepsWatchdog()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        (IReadOnlyList<DeviceId> rollback, IReadOnlyList<DeviceId> retain) =
            VrrpDeploymentPolicy.PlanPartialFailureActions([a, b], currentlyReachable: new HashSet<DeviceId> { a });
        Assert.Contains(a, rollback);
        Assert.Contains(b, retain);
        Assert.DoesNotContain(b, rollback);
    }

    [Fact]
    public void Ac12SplitMasterIsNotSimplified()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        VrrpRoleVector split = new()
        {
            Members =
            [
                Snapshot(a, VrrpMemberObservedState.Master, independentTraffic: false),
                Snapshot(b, VrrpMemberObservedState.Master, independentTraffic: false),
            ],
        };
        Assert.True(VrrpDeploymentPolicy.HasSplitMaster(split));
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification(split));
        Assert.StartsWith(DeploymentCodes.VrrpSplitMaster, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac13PartialCommitIsImpossible()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => VrrpDeploymentPolicy.EnsureFullCommitAllowed([a, b], new HashSet<DeviceId> { a }));
        Assert.StartsWith(DeploymentCodes.VrrpPartialCommitForbidden, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HappyPathCommitsOnlyWhenAllVerified()
    {
        (VrrpDeploymentResult result, _) = await HappyPathAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.Committed, result.State);
        Assert.Contains(result.Timeline, static t => t == "commit:all");
        Assert.False(result.PartialCommitAttempted);
    }

    private static async Task<(VrrpDeploymentResult Result, ScriptedCluster Cluster)> HappyPathAsync()
    {
        Node node = DeploymentTestFactory.VrrpWithMembers(out Device first, out Device second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedCluster cluster = new(
            new ScriptedMember(first.Id, VrrpMemberObservedState.Backup),
            new ScriptedMember(second.Id, VrrpMemberObservedState.Master));
        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            cluster.Members,
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));
        return (result, cluster);
    }

    private static VrrpMemberRoleSnapshot Snapshot(
        DeviceId deviceId,
        VrrpMemberObservedState state,
        bool independentTraffic)
        => new()
        {
            DeviceId = deviceId,
            HasIndependentRoutedTraffic = independentTraffic,
            Reachable = true,
            Instances =
            [
                new VrrpInstanceRoleFact
                {
                    Family = IpAddressFamily.IPv4,
                    Vrid = 1,
                    ObservedState = state,
                },
            ],
        };

    private sealed class ScriptedCluster
    {
        public ScriptedCluster(params ScriptedMember[] members) => Members = members;

        public ScriptedMember[] Members { get; }
    }

    private sealed class ScriptedMember : IVrrpMemberDeploymentRuntime
    {
        private VrrpMemberObservedState _state;

        public ScriptedMember(DeviceId deviceId, VrrpMemberObservedState state)
        {
            DeviceId = deviceId;
            _state = state;
        }

        public DeviceId DeviceId { get; }

        public bool Reachable { get; set; } = true;

        public bool Prechecked { get; private set; }

        public bool Staged { get; private set; }

        public bool Activated { get; private set; }

        public int RoleReadCount { get; private set; }

        public bool FlipRoleAfterFirstPeerActivation { get; set; }

        public bool BecomeUnreachableAfterPeerActivation { get; set; }

        public Func<bool>? PeerActivatedSignal { get; set; }

        public Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Reachable);

        public Task<VrrpMemberRoleSnapshot> ReadRoleSnapshotAsync(CancellationToken cancellationToken = default)
        {
            RoleReadCount++;
            MaybeMutateAfterPeer();
            return Task.FromResult(new VrrpMemberRoleSnapshot
            {
                DeviceId = DeviceId,
                HasIndependentRoutedTraffic = false,
                Reachable = Reachable,
                Instances =
                [
                    new VrrpInstanceRoleFact
                    {
                        Family = IpAddressFamily.IPv4,
                        Vrid = 1,
                        ObservedState = _state,
                    },
                ],
            });
        }

        public Task PrecheckAsync(CancellationToken cancellationToken = default)
        {
            Prechecked = true;
            return Task.CompletedTask;
        }

        public Task StageArtifactAsync(CancellationToken cancellationToken = default)
        {
            Staged = true;
            return Task.CompletedTask;
        }

        public Task ArmWatchdogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ActivateAsync(CancellationToken cancellationToken = default)
        {
            Activated = true;
            return Task.CompletedTask;
        }

        public Task VerifyAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DisarmWatchdogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackActivationAsync(CancellationToken cancellationToken = default)
        {
            Activated = false;
            return Task.CompletedTask;
        }

        private void MaybeMutateAfterPeer()
        {
            if (PeerActivatedSignal is null || !PeerActivatedSignal())
            {
                return;
            }

            if (FlipRoleAfterFirstPeerActivation && _state == VrrpMemberObservedState.Master)
            {
                _state = VrrpMemberObservedState.Backup;
            }

            if (BecomeUnreachableAfterPeerActivation)
            {
                Reachable = false;
            }
        }
    }
}
