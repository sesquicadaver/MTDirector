using System.Reflection;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Workflow;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Workflow;

/// <summary>Living Spec matrix for Issue Set M6-01 AC 1–10 (desired/committed/actual projection).</summary>
public sealed class DeviceStateProjectionLivingSpecTests
{
    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    private static DeviceHashState State(
        Guid deviceId,
        Hash256? desired,
        Hash256? committed,
        Hash256? actual,
        bool actualKnown = true,
        bool anchorKnown = true)
        => DeviceHashState.Create(
            new DeviceId(deviceId),
            desiredPolicyHash: desired,
            desiredArtifactHash: desired,
            lastCommittedPolicyHash: committed,
            lastCommittedArtifactHash: committed,
            actualManagedResourceHash: actual,
            actualKnown,
            anchorKnown,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Ac1PersistsDesiredCommittedAndActualHashes()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeDeviceHashStateStore store = new();
        FakeClock clock = new();
        Device device = Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.1", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);
        await devices.AddAsync(device);

        Hash256 desired = Hash(1);
        Hash256 committed = Hash(2);
        Hash256 actual = Hash(2);
        UpsertDeviceHashStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<DeviceHashStateView> written = await upsert.ExecuteAsync(
            new UpsertDeviceHashStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                DesiredArtifactHashHex = desired.ToString(),
                LastCommittedArtifactHashHex = committed.ToString(),
                ActualManagedResourceHashHex = actual.ToString(),
                ActualKnown = true,
                AnchorKnown = true,
            });
        Assert.True(written.IsSuccess, written.Error?.Message);

        ApplicationResult<DeviceHashStateView> loaded = await new GetDeviceHashStateUseCase(auth, store)
            .ExecuteAsync(new GetDeviceHashStateQuery { Actor = "tester", DeviceId = device.Id.Value });
        Assert.True(loaded.IsSuccess);
        Assert.Equal(desired.ToString(), loaded.Value!.DesiredArtifactHashHex);
        Assert.Equal(committed.ToString(), loaded.Value.LastCommittedArtifactHashHex);
        Assert.Equal(actual.ToString(), loaded.Value.ActualManagedResourceHashHex);
    }

    [Fact]
    public void Ac2SynchronizedWhenDesiredCommittedAndActualMatch()
    {
        Hash256 same = Hash(9);
        DeviceSyncClassification classification = DeviceHashStateClassifier.Classify(
            State(Guid.NewGuid(), same, same, same));
        Assert.Equal(DeviceSyncClassification.Synchronized, classification);
    }

    [Fact]
    public void Ac3PendingDeploymentIsNotDrift()
    {
        Hash256 desired = Hash(1);
        Hash256 committed = Hash(2);
        DeviceSyncClassification classification = DeviceHashStateClassifier.Classify(
            State(Guid.NewGuid(), desired, committed, actual: committed));
        Assert.Equal(DeviceSyncClassification.PendingDeployment, classification);
        Assert.NotEqual(DeviceSyncClassification.Drifted, classification);
    }

    [Fact]
    public void Ac4ActualDivergenceIsDrifted()
    {
        Hash256 committed = Hash(2);
        Hash256 actual = Hash(3);
        DeviceSyncClassification classification = DeviceHashStateClassifier.Classify(
            State(Guid.NewGuid(), desired: committed, committed, actual));
        Assert.Equal(DeviceSyncClassification.Drifted, classification);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Ac5UnknownAnchorOrActualIsRecoveryRequired(bool actualKnown, bool anchorKnown)
    {
        Hash256 same = Hash(4);
        DeviceSyncClassification classification = DeviceHashStateClassifier.Classify(
            State(Guid.NewGuid(), same, same, same, actualKnown, anchorKnown));
        Assert.Equal(DeviceSyncClassification.RecoveryRequired, classification);
    }

    [Fact]
    public void Ac6WorkflowStatusIsDerivedNotPersistedOnNodeEntity()
    {
        PropertyInfo[] nodeProps = typeof(NodeEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(nodeProps, static p => p.Name.Contains("Workflow", StringComparison.OrdinalIgnoreCase));

        NodeWorkflowFacts facts = new(
            recoveryRequired: false,
            ActiveEffectfulOperationKind.None,
            readinessBlockers: [],
            deviceHashStates:
            [
                State(Guid.Parse("11111111-1111-1111-1111-111111111111"), Hash(1), Hash(1), Hash(1)),
            ]);
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(facts);
        Assert.Equal(NodeWorkflowStatus.Synchronized, projection.NodeStatus);
    }

    [Theory]
    [InlineData(true, ActiveEffectfulOperationKind.Deployment, DeviceSyncClassification.Drifted, NodeWorkflowStatus.CaptureRequired, NodeWorkflowStatus.RecoveryRequired)]
    [InlineData(false, ActiveEffectfulOperationKind.Onboarding, DeviceSyncClassification.Drifted, NodeWorkflowStatus.CaptureRequired, NodeWorkflowStatus.OnboardingInProgress)]
    [InlineData(false, ActiveEffectfulOperationKind.Deployment, DeviceSyncClassification.Drifted, NodeWorkflowStatus.CaptureRequired, NodeWorkflowStatus.DeploymentInProgress)]
    [InlineData(false, ActiveEffectfulOperationKind.None, DeviceSyncClassification.Drifted, NodeWorkflowStatus.CaptureRequired, NodeWorkflowStatus.Drifted)]
    [InlineData(false, ActiveEffectfulOperationKind.None, DeviceSyncClassification.Synchronized, NodeWorkflowStatus.CaptureRequired, NodeWorkflowStatus.CaptureRequired)]
    [InlineData(false, ActiveEffectfulOperationKind.None, DeviceSyncClassification.PendingDeployment, null, NodeWorkflowStatus.PendingDeployment)]
    [InlineData(false, ActiveEffectfulOperationKind.None, DeviceSyncClassification.Synchronized, null, NodeWorkflowStatus.Synchronized)]
    public void Ac7PriorityOrderingMatchesE2ESpec(
        bool recovery,
        ActiveEffectfulOperationKind activeOp,
        DeviceSyncClassification deviceClass,
        NodeWorkflowStatus? readiness,
        NodeWorkflowStatus expected)
    {
        Hash256 a = Hash(1);
        Hash256 b = Hash(2);
        DeviceHashState state = deviceClass switch
        {
            DeviceSyncClassification.Synchronized => State(Guid.NewGuid(), a, a, a),
            DeviceSyncClassification.PendingDeployment => State(Guid.NewGuid(), a, b, b),
            DeviceSyncClassification.Drifted => State(Guid.NewGuid(), a, a, b),
            DeviceSyncClassification.RecoveryRequired => State(Guid.NewGuid(), a, a, a, actualKnown: false),
            _ => State(Guid.NewGuid(), null, null, null),
        };
        IReadOnlyList<NodeWorkflowStatus> blockers = readiness is null ? [] : [readiness.Value];
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(
            new NodeWorkflowFacts(recovery, activeOp, blockers, [state]));
        Assert.Equal(expected, projection.NodeStatus);
    }

    [Fact]
    public void Ac8VrrpAggregatesWithoutDroppingPerDeviceState()
    {
        Guid deviceA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid deviceB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Hash256 same = Hash(1);
        Hash256 other = Hash(2);
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(
            new NodeWorkflowFacts(
                recoveryRequired: false,
                ActiveEffectfulOperationKind.None,
                readinessBlockers: [],
                deviceHashStates:
                [
                    State(deviceB, same, same, same),
                    State(deviceA, same, same, other),
                ]));

        Assert.Equal(NodeWorkflowStatus.Drifted, projection.NodeStatus);
        Assert.Equal(2, projection.Devices.Count);
        Assert.Equal(deviceA, projection.Devices[0].DeviceId.Value);
        Assert.Equal(deviceB, projection.Devices[1].DeviceId.Value);
        Assert.Equal(DeviceSyncClassification.Drifted, projection.Devices[0].SyncClassification);
        Assert.Equal(DeviceSyncClassification.Synchronized, projection.Devices[1].SyncClassification);
    }

    [Fact]
    public void Ac9ProjectionIsDeterministicAcrossInputPermutation()
    {
        Guid deviceA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid deviceB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Hash256 same = Hash(1);
        Hash256 other = Hash(2);
        DeviceHashState[] order1 =
        [
            State(deviceA, same, same, other),
            State(deviceB, same, same, same),
        ];
        DeviceHashState[] order2 =
        [
            State(deviceB, same, same, same),
            State(deviceA, same, same, other),
        ];
        NodeWorkflowProjection left = NodeWorkflowStatusProjector.Project(
            new NodeWorkflowFacts(false, ActiveEffectfulOperationKind.None, [], order1));
        NodeWorkflowProjection right = NodeWorkflowStatusProjector.Project(
            new NodeWorkflowFacts(false, ActiveEffectfulOperationKind.None, [], order2));

        Assert.Equal(left.NodeStatus, right.NodeStatus);
        Assert.Equal(
            left.Devices.Select(static d => d.DeviceId.Value).ToArray(),
            right.Devices.Select(static d => d.DeviceId.Value).ToArray());
        Assert.Equal(
            left.Devices.Select(static d => d.SyncClassification).ToArray(),
            right.Devices.Select(static d => d.SyncClassification).ToArray());
    }

    [Fact]
    public void Ac10DesktopSurfacesDesiredCommittedAndActualHashes()
    {
        Type item = typeof(InventoryTreeItem);
        Assert.NotNull(item.GetProperty(nameof(InventoryTreeItem.DesiredHashText)));
        Assert.NotNull(item.GetProperty(nameof(InventoryTreeItem.CommittedHashText)));
        Assert.NotNull(item.GetProperty(nameof(InventoryTreeItem.ActualHashText)));
        Assert.NotNull(item.GetProperty(nameof(InventoryTreeItem.WorkflowStatusText)));

        Type vm = typeof(InventoryNodeViewModel);
        Assert.NotNull(vm.GetProperty(nameof(InventoryNodeViewModel.DesiredHashText)));
        Assert.NotNull(vm.GetProperty(nameof(InventoryNodeViewModel.CommittedHashText)));
        Assert.NotNull(vm.GetProperty(nameof(InventoryNodeViewModel.ActualHashText)));
        Assert.NotNull(vm.GetProperty(nameof(InventoryNodeViewModel.WorkflowStatusText)));

        InventoryTreeItem deviceItem = new()
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.NewGuid(),
            DisplayName = "r1",
            DesiredHashText = "aabbccddeeff",
            CommittedHashText = "112233445566",
            ActualHashText = "77889900aabb",
        };
        InventoryNodeViewModel viewModel = new(deviceItem);
        Assert.Contains("Desired:", viewModel.DetailSummary, StringComparison.Ordinal);
        Assert.Contains("Committed:", viewModel.DetailSummary, StringComparison.Ordinal);
        Assert.Contains("Actual:", viewModel.DetailSummary, StringComparison.Ordinal);
        Assert.Contains(deviceItem.DesiredHashText, viewModel.DetailSummary, StringComparison.Ordinal);
    }
}
