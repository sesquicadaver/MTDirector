using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W3.3 Watch + W4.2 VRRP pair Create plan + CONT-01 Rollback Watch.</summary>
public sealed class DeploymentViewModelTests
{
    [Fact]
    public async Task StartWatchesProgressAndPrefersStreamOverStartTimeline()
    {
        Guid planId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");
        Guid operationId = Guid.Parse("33333333-4444-5555-6666-777777777777");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        FakeDeploymentClient client = new()
        {
            StartResponse = new DeploymentOperationSummary
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
                PlanId = DesktopProtoUuid.FromGuid(planId),
                State = DeploymentOperationState.Activating,
                Timeline = { "from-start-only" },
            },
            WatchEvents =
            [
                new DeploymentProgress
                {
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    State = DeploymentOperationState.Activating,
                    TimelineEntry = "activating anchors",
                },
                new DeploymentProgress
                {
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    State = DeploymentOperationState.Committed,
                    TimelineEntry = "committed",
                },
            ],
        };

        using DeploymentViewModel vm = new(client, connection, inventory)
        {
            PlanId = planId,
            PlanHash = Hash("plan"),
        };

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.StartCalls);
        Assert.Equal(1, client.WatchCalls);
        Assert.Equal(operationId, client.WatchedOperationId);
        Assert.Equal(operationId, vm.OperationId);
        Assert.Equal("Operation Committed.", vm.StatusText);
        Assert.Equal(
            ["Activating: activating anchors", "Committed: committed"],
            vm.ProgressLines.ToArray());
        Assert.DoesNotContain("from-start-only", vm.ProgressLines);
    }

    [Fact]
    public async Task RollbackWatchesProgressAndPrefersStreamOverRollbackTimeline()
    {
        Guid operationId = Guid.Parse("33333333-4444-5555-6666-777777777777");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        FakeDeploymentClient client = new()
        {
            RollbackResponse = new DeploymentOperationSummary
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
                State = DeploymentOperationState.RollingBack,
                Timeline = { "from-rollback-only" },
            },
            WatchEvents =
            [
                new DeploymentProgress
                {
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    State = DeploymentOperationState.RollingBack,
                    TimelineEntry = "rolling back anchors",
                },
                new DeploymentProgress
                {
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    State = DeploymentOperationState.RolledBack,
                    TimelineEntry = "rolled back",
                },
            ],
        };

        using DeploymentViewModel vm = new(client, connection, inventory)
        {
            OperationId = operationId,
        };
        vm.ProgressLines.Add("prior-start-line");

        await vm.RollbackCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.RollbackCalls);
        Assert.Equal(1, client.WatchCalls);
        Assert.Equal(operationId, client.WatchedOperationId);
        Assert.Equal("Rollback RolledBack.", vm.StatusText);
        Assert.Equal(
            ["RollingBack: rolling back anchors", "RolledBack: rolled back"],
            vm.ProgressLines.ToArray());
        Assert.DoesNotContain("from-rollback-only", vm.ProgressLines);
        Assert.DoesNotContain("prior-start-line", vm.ProgressLines);
    }

    [Fact]
    public async Task RollbackFallsBackToTimelineWhenWatchEmpty()
    {
        Guid operationId = Guid.Parse("33333333-4444-5555-6666-777777777777");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        FakeDeploymentClient client = new()
        {
            RollbackResponse = new DeploymentOperationSummary
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
                State = DeploymentOperationState.RolledBack,
                Timeline = { "rollback-snapshot" },
            },
        };

        using DeploymentViewModel vm = new(client, connection, inventory)
        {
            OperationId = operationId,
        };
        vm.ProgressLines.Add("prior-start-line");

        await vm.RollbackCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.RollbackCalls);
        Assert.Equal(1, client.WatchCalls);
        Assert.Equal("Rollback RolledBack.", vm.StatusText);
        Assert.Equal(["prior-start-line", "rollback-snapshot"], vm.ProgressLines.ToArray());
    }

    [Fact]
    public async Task CreatePlanIncludesAllVrrpMembersNotSilentFirstChild()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceB = Guid.Parse("22222222-3333-4444-5555-666666666666");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "edge-pair",
                    NodeKindText = "Vrrp",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceA,
                            DisplayName = "r1",
                        },
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceB,
                            DisplayName = "r2",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0];
        FakeDeploymentClient client = new();
        using DeploymentViewModel vm = new(client, connection, inventory);

        Assert.True(vm.HasVrrpPairTarget);
        Assert.Equal(InventoryOpsSelection.VrrpPairHint, vm.TargetHint);

        await vm.CreatePlanCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(nodeId, client.LastNodeId);
        Assert.Equal([deviceA, deviceB], client.LastDeviceIds);
        Assert.NotEqual(deviceA, deviceB);
        Assert.Equal(2, client.LastDeviceIds.Count);
    }

    [Fact]
    public async Task CreatePlanBindsTypedSemanticDiffKindPathBeforeAfter()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        string before = Convert.ToHexString(Enumerable.Repeat((byte)0x11, 32).ToArray());
        string after = Convert.ToHexString(Enumerable.Repeat((byte)0x22, 32).ToArray());
        string hashDelta = $"device:{deviceId:D}:artifact {before[..12]}… → {after[..12]}…";
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "edge",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceId,
                            DisplayName = "r1",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0];
        FakeDeploymentClient client = new()
        {
            CreatePlanOverride = new DeploymentPlanSummary
            {
                PlanId = DesktopProtoUuid.FromGuid(Guid.Parse("dddddddd-eeee-ffff-aaaa-111111111111")),
                PlanHash = Hash("plan"),
                SemanticDiff =
                {
                    new DeploymentSemanticDiffEntry
                    {
                        Kind = DeploymentSemanticDiffKind.ArtifactChanged,
                        Path = $"device/{deviceId:D}/artifact",
                        DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                        Before = before,
                        After = after,
                        HashDelta = hashDelta,
                    },
                },
                SemanticDiffEntries = { hashDelta },
            },
        };
        using DeploymentViewModel vm = new(client, connection, inventory);

        await vm.CreatePlanCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Single(vm.SemanticDiffRows);
        Assert.Equal("ARTIFACT_CHANGED", vm.SemanticDiffRows[0].KindText);
        Assert.Equal($"device/{deviceId:D}/artifact", vm.SemanticDiffRows[0].PathText);
        Assert.Equal(before, vm.SemanticDiffRows[0].BeforeText);
        Assert.Equal(after, vm.SemanticDiffRows[0].AfterText);
        Assert.Equal(hashDelta, vm.SemanticDiffRows[0].HashDeltaText);
        Assert.Equal([hashDelta], vm.SemanticDiffLines.ToArray());
    }

    [Fact]
    public async Task CreatePlanWhenVrrpMemberSelectedStillUsesPair()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceB = Guid.Parse("22222222-3333-4444-5555-666666666666");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "edge-pair",
                    NodeKindText = "Vrrp",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceA,
                            DisplayName = "r1",
                        },
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceB,
                            DisplayName = "r2",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0].Children[1];
        FakeDeploymentClient client = new();
        using DeploymentViewModel vm = new(client, connection, inventory);

        await vm.CreatePlanCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(nodeId, client.LastNodeId);
        Assert.Equal([deviceA, deviceB], client.LastDeviceIds);
    }

    private static Sha256 Hash(string seed)
        => new() { Value = ByteString.CopyFrom(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed))) };

    private sealed class FakeConnection : IControllerConnectionService
    {
        public ControllerConnectionState State { get; set; } = ControllerConnectionState.Connected;

        public string? LastError => null;

        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyTreeService : IInventoryTreeService
    {
        public InventoryTreeLoadResult Current { get; } = new()
        {
            Roots = [],
            Succeeded = true,
            IsCached = false,
            IsRefreshing = false,
        };

        public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);
    }

    private sealed class FakeDeploymentClient : IDeploymentServiceClient
    {
        public DeploymentOperationSummary StartResponse { get; init; } = new();

        public IReadOnlyList<DeploymentProgress> WatchEvents { get; init; } = [];

        public DeploymentOperationSummary RollbackResponse { get; init; } = new();

        public int StartCalls { get; private set; }

        public int WatchCalls { get; private set; }

        public int RollbackCalls { get; private set; }

        public Guid WatchedOperationId { get; private set; }

        public Guid LastNodeId { get; private set; }

        public List<Guid> LastDeviceIds { get; private set; } = [];

        public DeploymentPlanSummary? CreatePlanOverride { get; init; }

        public Task<DeploymentPlanSummary> CreatePlanAsync(
            Guid nodeId,
            Sha256 logicalPolicyHash,
            Sha256 analysisBundleHash,
            Sha256 topologyHash,
            IReadOnlyList<DeploymentDevicePlanInput> devices,
            CancellationToken cancellationToken = default)
        {
            LastNodeId = nodeId;
            LastDeviceIds = devices.Select(d => DesktopProtoUuid.ToGuid(d.DeviceId)).ToList();
            if (CreatePlanOverride is not null)
            {
                return Task.FromResult(CreatePlanOverride);
            }

            return Task.FromResult(new DeploymentPlanSummary
            {
                PlanId = DesktopProtoUuid.FromGuid(Guid.Parse("dddddddd-eeee-ffff-aaaa-111111111111")),
                PlanHash = logicalPolicyHash,
            });
        }

        public Task<DeploymentOperationSummary> StartAsync(
            Guid planId,
            Sha256 planHash,
            IReadOnlyList<DeploymentPacketPathPairFact> packetPathPairs,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCalls++;
            return Task.FromResult(StartResponse);
        }

        public async IAsyncEnumerable<DeploymentProgress> WatchAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WatchCalls++;
            WatchedOperationId = operationId;
            foreach (DeploymentProgress progress in WatchEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return progress;
                await Task.Yield();
            }
        }

        public Task<DeploymentOperationSummary> RollbackAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RollbackCalls++;
            return Task.FromResult(RollbackResponse);
        }

        public Task<DeploymentRecoveryStatus> GetRecoveryStatusAsync(
            Guid nodeId,
            Guid? operationId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
