using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Workflow;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using InventoryNodeStatus = Mfc.Domain.Inventory.NodeStatus;

namespace Mfc.UnitTests.Workflow;

public sealed class WorkflowProjectionCoverageTests
{
    [Fact]
    public void DeviceHashStateReconstituteRejectsZeroRowVersion()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() => DeviceHashState.Reconstitute(
            new DeviceId(Guid.NewGuid()),
            desiredPolicyHash: null,
            desiredArtifactHash: null,
            lastCommittedPolicyHash: null,
            lastCommittedArtifactHash: null,
            actualManagedResourceHash: null,
            actualKnown: true,
            anchorKnown: true,
            updatedAtUtc: DateTimeOffset.UtcNow,
            rowVersion: 0));
        Assert.Contains("row_version", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceHashStateEqualityAndHashCodeHandleNullHashBranches()
    {
        Guid deviceId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        DeviceHashState left = DeviceHashState.Create(
            new DeviceId(deviceId),
            desiredPolicyHash: null,
            desiredArtifactHash: Hash(1),
            lastCommittedPolicyHash: null,
            lastCommittedArtifactHash: Hash(2),
            actualManagedResourceHash: Hash(2),
            actualKnown: true,
            anchorKnown: true,
            updatedAtUtc: now);
        DeviceHashState equal = DeviceHashState.Create(
            new DeviceId(deviceId),
            desiredPolicyHash: null,
            desiredArtifactHash: Hash(1),
            lastCommittedPolicyHash: null,
            lastCommittedArtifactHash: Hash(2),
            actualManagedResourceHash: Hash(2),
            actualKnown: true,
            anchorKnown: true,
            updatedAtUtc: now);
        DeviceHashState mismatch = DeviceHashState.Create(
            new DeviceId(deviceId),
            desiredPolicyHash: Hash(9),
            desiredArtifactHash: Hash(1),
            lastCommittedPolicyHash: null,
            lastCommittedArtifactHash: Hash(2),
            actualManagedResourceHash: Hash(2),
            actualKnown: true,
            anchorKnown: true,
            updatedAtUtc: now);

        Assert.True(left.Equals(equal));
        Assert.Equal(left.GetHashCode(), equal.GetHashCode());
        Assert.False(left.Equals(mismatch));
        Assert.False(left.Equals((DeviceHashState?)null));
        Assert.False(left.Equals(new object()));
    }

    [Fact]
    public void DeviceHashStateClassifierCoversIncompleteAndRecoveryBranches()
    {
        DeviceSyncClassification noBaseline = DeviceHashStateClassifier.Classify(State(
            Guid.NewGuid(),
            desiredArtifactHash: Hash(1),
            committedArtifactHash: null,
            actualManagedResourceHash: null));
        DeviceSyncClassification missingActual = DeviceHashStateClassifier.Classify(State(
            Guid.NewGuid(),
            desiredArtifactHash: Hash(1),
            committedArtifactHash: Hash(2),
            actualManagedResourceHash: null));
        DeviceSyncClassification missingDesired = DeviceHashStateClassifier.Classify(State(
            Guid.NewGuid(),
            desiredArtifactHash: null,
            committedArtifactHash: Hash(3),
            actualManagedResourceHash: Hash(3)));

        Assert.Equal(DeviceSyncClassification.Incomplete, noBaseline);
        Assert.Equal(DeviceSyncClassification.RecoveryRequired, missingActual);
        Assert.Equal(DeviceSyncClassification.Incomplete, missingDesired);
    }

    [Fact]
    public void NodeWorkflowFactsRejectNonReadinessBlockers()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() => new NodeWorkflowFacts(
            recoveryRequired: false,
            ActiveEffectfulOperationKind.None,
            readinessBlockers: [NodeWorkflowStatus.Synchronized],
            deviceHashStates: []));
        Assert.Contains("readiness blockers", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeWorkflowStatusProjectorReturnsInventoryIncompleteWhenDevicesAreOnlyIncomplete()
    {
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(new NodeWorkflowFacts(
            recoveryRequired: false,
            ActiveEffectfulOperationKind.None,
            readinessBlockers: [],
            deviceHashStates:
            [
                State(Guid.NewGuid(), desiredArtifactHash: null, committedArtifactHash: null, actualManagedResourceHash: null),
            ]));

        Assert.Equal(NodeWorkflowStatus.InventoryIncomplete, projection.NodeStatus);
        Assert.Single(projection.Devices);
        Assert.Equal(DeviceSyncClassification.Incomplete, projection.Devices[0].SyncClassification);
        Assert.Null(projection.Devices[0].ContributingStatus);
    }

    [Theory]
    [InlineData(NodeWorkflowStatus.InventoryIncomplete)]
    [InlineData(NodeWorkflowStatus.ConnectionInvalid)]
    [InlineData(NodeWorkflowStatus.CaptureRequired)]
    [InlineData(NodeWorkflowStatus.TopologyBlocked)]
    [InlineData(NodeWorkflowStatus.OnboardingRequired)]
    [InlineData(NodeWorkflowStatus.PolicyRequired)]
    [InlineData(NodeWorkflowStatus.AnalysisRequired)]
    [InlineData(NodeWorkflowStatus.AnalysisBlocked)]
    public void NodeWorkflowStatusProjectorReturnsEachReadinessRank(NodeWorkflowStatus blocker)
    {
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(new NodeWorkflowFacts(
            recoveryRequired: false,
            ActiveEffectfulOperationKind.None,
            readinessBlockers: [blocker],
            deviceHashStates: [SynchronizedState(Guid.NewGuid())]));

        Assert.Equal(blocker, projection.NodeStatus);
    }

    [Fact]
    public void NodeWorkflowStatusProjectorSelectsHighestPriorityReadinessAcrossMultipleBlockers()
    {
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(new NodeWorkflowFacts(
            recoveryRequired: false,
            ActiveEffectfulOperationKind.None,
            readinessBlockers:
            [
                NodeWorkflowStatus.AnalysisBlocked,
                NodeWorkflowStatus.PolicyRequired,
                NodeWorkflowStatus.TopologyBlocked,
            ],
            deviceHashStates: [SynchronizedState(Guid.NewGuid())]));

        Assert.Equal(NodeWorkflowStatus.TopologyBlocked, projection.NodeStatus);
    }

    [Fact]
    public void NodeWorkflowStatusProjectorReturnsOnboardingInProgressForActiveOperation()
    {
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(new NodeWorkflowFacts(
            recoveryRequired: false,
            ActiveEffectfulOperationKind.Onboarding,
            readinessBlockers: [],
            deviceHashStates: [SynchronizedState(Guid.NewGuid())]));

        Assert.Equal(NodeWorkflowStatus.OnboardingInProgress, projection.NodeStatus);
    }

    [Fact]
    public async Task UpsertDeviceHashStateUseCaseReturnsForbiddenWhenWritePermissionMissing()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryWrite);
        UpsertDeviceHashStateUseCase useCase = new(auth, new FakeDeviceStore(), new FakeDeviceHashStateStore(), new FakeClock());

        ApplicationResult<DeviceHashStateView> result = await useCase.ExecuteAsync(new UpsertDeviceHashStateCommand
        {
            Actor = "guest",
            DeviceId = Guid.NewGuid(),
            ActualKnown = true,
            AnchorKnown = true,
        });

        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertDeviceHashStateUseCaseReturnsNotFoundWhenDeviceMissing()
    {
        UpsertDeviceHashStateUseCase useCase = new(
            new FakeAuthorizationBoundary(),
            new FakeDeviceStore(),
            new FakeDeviceHashStateStore(),
            new FakeClock());

        ApplicationResult<DeviceHashStateView> result = await useCase.ExecuteAsync(new UpsertDeviceHashStateCommand
        {
            Actor = "tester",
            DeviceId = Guid.NewGuid(),
            ActualKnown = true,
            AnchorKnown = true,
        });

        Assert.Equal("not_found", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertDeviceHashStateUseCaseReturnsValidationForInvalidHashHex()
    {
        FakeDeviceStore devices = new();
        Device device = CreateDeviceEntity(Guid.NewGuid(), Guid.NewGuid(), ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        await devices.AddAsync(device);
        UpsertDeviceHashStateUseCase useCase = new(
            new FakeAuthorizationBoundary(),
            devices,
            new FakeDeviceHashStateStore(),
            new FakeClock());

        ApplicationResult<DeviceHashStateView> result = await useCase.ExecuteAsync(new UpsertDeviceHashStateCommand
        {
            Actor = "tester",
            DeviceId = device.Id.Value,
            DesiredArtifactHashHex = "not-hex",
            ActualKnown = true,
            AnchorKnown = true,
        });

        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertDeviceHashStateUseCaseCreatesThenUpdatesExistingState()
    {
        FakeDeviceStore devices = new();
        FakeDeviceHashStateStore hashStates = new();
        FakeClock clock = new();
        Device device = CreateDeviceEntity(Guid.NewGuid(), Guid.NewGuid(), ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        await devices.AddAsync(device);
        UpsertDeviceHashStateUseCase useCase = new(new FakeAuthorizationBoundary(), devices, hashStates, clock);

        ApplicationResult<DeviceHashStateView> created = await useCase.ExecuteAsync(new UpsertDeviceHashStateCommand
        {
            Actor = "tester",
            DeviceId = device.Id.Value,
            DesiredArtifactHashHex = Hash(1).ToString(),
            LastCommittedArtifactHashHex = Hash(2).ToString(),
            ActualManagedResourceHashHex = Hash(2).ToString(),
            ActualKnown = true,
            AnchorKnown = true,
        });
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        ApplicationResult<DeviceHashStateView> updated = await useCase.ExecuteAsync(new UpsertDeviceHashStateCommand
        {
            Actor = "tester",
            DeviceId = device.Id.Value,
            DesiredArtifactHashHex = Hash(3).ToString(),
            LastCommittedArtifactHashHex = Hash(3).ToString(),
            ActualManagedResourceHashHex = Hash(3).ToString(),
            ActualKnown = true,
            AnchorKnown = true,
        });

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(1ul, created.Value!.RowVersion);
        Assert.Equal(2ul, updated.Value!.RowVersion);
        Assert.Equal(Hash(3).ToString(), updated.Value.DesiredArtifactHashHex);

        DeviceHashState? persisted = await hashStates.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Equal(2ul, persisted!.RowVersion);
        Assert.Equal(Hash(3), persisted.ActualManagedResourceHash);
    }

    [Fact]
    public async Task GetDeviceHashStateUseCaseHandlesForbiddenAndNotFound()
    {
        FakeAuthorizationBoundary deniedAuth = new();
        deniedAuth.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        GetDeviceHashStateUseCase forbiddenUseCase = new(deniedAuth, new FakeDeviceHashStateStore());
        ApplicationResult<DeviceHashStateView> forbidden = await forbiddenUseCase.ExecuteAsync(new GetDeviceHashStateQuery
        {
            Actor = "guest",
            DeviceId = Guid.NewGuid(),
        });

        GetDeviceHashStateUseCase missingUseCase = new(new FakeAuthorizationBoundary(), new FakeDeviceHashStateStore());
        ApplicationResult<DeviceHashStateView> missing = await missingUseCase.ExecuteAsync(new GetDeviceHashStateQuery
        {
            Actor = "tester",
            DeviceId = Guid.NewGuid(),
        });

        Assert.Equal("forbidden", forbidden.Error!.Code);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseHandlesForbiddenAndNotFound()
    {
        WorkflowHarness forbiddenHarness = new();
        forbiddenHarness.Auth.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        ApplicationResult<NodeWorkflowProjectionView> forbidden = await forbiddenHarness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "guest", NodeId = Guid.NewGuid() });

        WorkflowHarness missingHarness = new();
        ApplicationResult<NodeWorkflowProjectionView> missing = await missingHarness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = Guid.NewGuid() });

        Assert.Equal("forbidden", forbidden.Error!.Code);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseSynthesizesIncompleteStateForDevicesWithoutRows()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.Managed, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await AddCompanyBindingAsync(harness.Approvals);

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.InventoryIncomplete, result.Value!.NodeStatus);
        Assert.Single(result.Value.Devices);
        Assert.Equal(DeviceSyncClassification.Incomplete, result.Value.Devices[0].SyncClassification);
        Assert.Equal(1ul, result.Value.Devices[0].HashState.RowVersion);
        Assert.Null(result.Value.Devices[0].ContributingStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsRecoveryRequiredFromManagementState()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.RecoveryRequired, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await AddCompanyBindingAsync(harness.Approvals);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.RecoveryRequired, result.Value!.NodeStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsOnboardingInProgressForNonterminalOperation()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.Managed, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await AddCompanyBindingAsync(harness.Approvals);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));
        await harness.Onboarding.AddOperationAsync(OnboardingOperation.Reconstitute(
            OnboardingOperationId.New(),
            node.Id,
            OnboardingPlanId.New(),
            OnboardingOperationState.Prechecking,
            UserId.New(),
            startedAtUtc: DateTimeOffset.UtcNow,
            completedAtUtc: null,
            errorCode: null,
            rowVersion: 1,
            createdAtUtc: DateTimeOffset.UtcNow,
            updatedAtUtc: DateTimeOffset.UtcNow));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.OnboardingInProgress, result.Value!.NodeStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsDeploymentInProgressForNonterminalOperation()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.Managed, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await AddCompanyBindingAsync(harness.Approvals);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));
        await harness.Deployments.AddOperationAsync(DeploymentOperation.Reconstitute(
            DeploymentOperationId.New(),
            node.Id,
            DeploymentPlanId.New(),
            DeploymentOperationState.Prechecking,
            UserId.New(),
            startedAtUtc: DateTimeOffset.UtcNow,
            completedAtUtc: null,
            errorCode: null,
            rowVersion: 1,
            createdAtUtc: DateTimeOffset.UtcNow,
            updatedAtUtc: DateTimeOffset.UtcNow));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.DeploymentInProgress, result.Value!.NodeStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsInventoryIncompleteWhenNodeHasNoDevices()
    {
        WorkflowHarness harness = new();
        Node node = CreateNodeEntity(Guid.NewGuid(), ManagementState.Managed);
        await harness.Nodes.AddAsync(node);
        await AddCompanyBindingAsync(harness.Approvals);

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.InventoryIncomplete, result.Value!.NodeStatus);
        Assert.Empty(result.Value.Devices);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsConnectionInvalidWhenAnyEnabledDeviceLacksConnection()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.Managed, device);
        await SeedAsync(harness, node, device);
        await AddCompanyBindingAsync(harness.Approvals);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.ConnectionInvalid, result.Value!.NodeStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsCaptureRequiredWhenEnabledDeviceHasNoCapture()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: null);
        Node node = CreateNodeEntity(nodeId, ManagementState.Managed, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await AddCompanyBindingAsync(harness.Approvals);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.CaptureRequired, result.Value!.NodeStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsOnboardingRequiredForUnmanagedNodeAndDevice()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Unmanaged, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.Unmanaged, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await AddCompanyBindingAsync(harness.Approvals);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.OnboardingRequired, result.Value!.NodeStatus);
    }

    [Fact]
    public async Task ProjectNodeWorkflowUseCaseReturnsPolicyRequiredWhenNoBindingsExist()
    {
        WorkflowHarness harness = new();
        Guid nodeId = Guid.NewGuid();
        Device device = CreateDeviceEntity(Guid.NewGuid(), nodeId, ManagementState.Managed, lastCompletedCaptureId: Guid.NewGuid());
        Node node = CreateNodeEntity(nodeId, ManagementState.Managed, device);
        await SeedAsync(harness, node, device);
        AddConnection(harness.Connections, device.Id);
        await harness.HashStates.UpsertAsync(SynchronizedState(device.Id.Value));

        ApplicationResult<NodeWorkflowProjectionView> result = await harness.CreateUseCase().ExecuteAsync(
            new ProjectNodeWorkflowQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NodeWorkflowStatus.PolicyRequired, result.Value!.NodeStatus);
    }

    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    private static DeviceHashState State(
        Guid deviceId,
        Hash256? desiredArtifactHash,
        Hash256? committedArtifactHash,
        Hash256? actualManagedResourceHash,
        bool actualKnown = true,
        bool anchorKnown = true,
        Hash256? desiredPolicyHash = null,
        Hash256? committedPolicyHash = null)
        => DeviceHashState.Create(
            new DeviceId(deviceId),
            desiredPolicyHash,
            desiredArtifactHash,
            committedPolicyHash,
            committedArtifactHash,
            actualManagedResourceHash,
            actualKnown,
            anchorKnown,
            DateTimeOffset.UtcNow);

    private static DeviceHashState SynchronizedState(Guid deviceId)
    {
        Hash256 hash = Hash(7);
        return State(
            deviceId,
            desiredArtifactHash: hash,
            committedArtifactHash: hash,
            actualManagedResourceHash: hash,
            desiredPolicyHash: hash,
            committedPolicyHash: hash);
    }

    private static Node CreateNodeEntity(Guid nodeId, ManagementState managementState, params Device[] devices)
    {
        Node node = Node.Reconstitute(
            new NodeId(nodeId),
            new SiteId(Guid.NewGuid()),
            NonEmptyName.Create("node"),
            NodeKind.Router,
            DeclaredUplinkMode.One,
            InventoryNodeStatus.Draft,
            managementState,
            rowVersion: 1);
        foreach (Device device in devices)
        {
            node.AttachDevice(device);
        }

        return node;
    }

    private static Device CreateDeviceEntity(
        Guid deviceId,
        Guid nodeId,
        ManagementState managementState,
        Guid? lastCompletedCaptureId)
        => Device.Reconstitute(
            new DeviceId(deviceId),
            new NodeId(nodeId),
            NonEmptyName.Create("device"),
            ManagementEndpoint.Create("192.0.2.10", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            managementState,
            rowVersion: 1,
            lastCompletedCaptureId: lastCompletedCaptureId);

    private static void AddConnection(FakeConnectionProfileReadStore connections, DeviceId deviceId)
        => connections.ByDevice[deviceId.Value] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

    private static Task AddCompanyBindingAsync(FakePolicyApprovalStore approvals)
        => approvals.AddBindingAsync(PolicyDesiredBinding.Reconstitute(
            PolicyBindingId.New(),
            PolicyBindingScope.Company,
            scopeId: null,
            PolicyId.New(),
            PolicyRevisionId.New(),
            PolicyAnalysisRunId.New(),
            Hash(8),
            PolicyBindingState.Active,
            validFromUtc: null,
            validUntilUtc: null,
            rowVersion: 1,
            createdAtUtc: DateTimeOffset.UtcNow,
            updatedAtUtc: DateTimeOffset.UtcNow));

    private static async Task SeedAsync(WorkflowHarness harness, Node node, params Device[] devices)
    {
        await harness.Nodes.AddAsync(node);
        foreach (Device device in devices)
        {
            await harness.Devices.AddAsync(device);
        }
    }

    private sealed class WorkflowHarness
    {
        public FakeAuthorizationBoundary Auth { get; } = new();

        public FakeNodeStore Nodes { get; } = new();

        public FakeDeviceStore Devices { get; } = new();

        public FakeDeviceHashStateStore HashStates { get; } = new();

        public FakeConnectionProfileReadStore Connections { get; } = new();

        public FakeOnboardingStore Onboarding { get; } = new();

        public FakeDeploymentStore Deployments { get; } = new();

        public FakePolicyApprovalStore Approvals { get; } = new();

        public ProjectNodeWorkflowUseCase CreateUseCase()
            => new(Auth, Nodes, Devices, HashStates, Connections, Onboarding, Deployments, Approvals);
    }
}
