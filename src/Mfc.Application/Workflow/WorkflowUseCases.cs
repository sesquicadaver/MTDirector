using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Workflow;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Workflow;

/// <summary>Upserts persisted desired / committed / actual hashes for one Device.</summary>
public sealed class UpsertDeviceHashStateCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }

    public string? DesiredPolicyHashHex { get; init; }

    public string? DesiredArtifactHashHex { get; init; }

    public string? LastCommittedPolicyHashHex { get; init; }

    public string? LastCommittedArtifactHashHex { get; init; }

    public string? ActualManagedResourceHashHex { get; init; }

    public required bool ActualKnown { get; init; }

    public required bool AnchorKnown { get; init; }
}

/// <summary>Stores device hash projection rows (M6-01 AC1).</summary>
public sealed class UpsertDeviceHashStateUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IDeviceHashStateStore _hashStates;
    private readonly IClock _clock;

    public UpsertDeviceHashStateUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IDeviceHashStateStore hashStates,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _devices = devices;
        _hashStates = hashStates;
        _clock = clock;
    }

    public async Task<ApplicationResult<DeviceHashStateView>> ExecuteAsync(
        UpsertDeviceHashStateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        DeviceId deviceId = new(command.DeviceId);
        Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Device '{command.DeviceId}' not found."));
        }

        Hash256? desiredPolicy;
        Hash256? desiredArtifact;
        Hash256? committedPolicy;
        Hash256? committedArtifact;
        Hash256? actual;
        try
        {
            desiredPolicy = ParseOptionalHash(command.DesiredPolicyHashHex);
            desiredArtifact = ParseOptionalHash(command.DesiredArtifactHashHex);
            committedPolicy = ParseOptionalHash(command.LastCommittedPolicyHashHex);
            committedArtifact = ParseOptionalHash(command.LastCommittedArtifactHashHex);
            actual = ParseOptionalHash(command.ActualManagedResourceHashHex);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        DateTimeOffset now = _clock.UtcNow;
        DeviceHashState? existing = await _hashStates.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        DeviceHashState state = existing is null
            ? DeviceHashState.Create(
                deviceId,
                desiredPolicy,
                desiredArtifact,
                committedPolicy,
                committedArtifact,
                actual,
                command.ActualKnown,
                command.AnchorKnown,
                now)
            : existing.With(
                desiredPolicy,
                desiredArtifact,
                committedPolicy,
                committedArtifact,
                actual,
                command.ActualKnown,
                command.AnchorKnown,
                now);

        await _hashStates.UpsertAsync(state, cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(WorkflowViewMapper.ToView(state));
    }

    private static Hash256? ParseOptionalHash(string? hex)
        => string.IsNullOrWhiteSpace(hex) ? null : Hash256.ParseHex(hex);
}

/// <summary>Loads one device hash-state row.</summary>
public sealed class GetDeviceHashStateQuery
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

/// <summary>Reads persisted device hash projection (M6-01 AC1).</summary>
public sealed class GetDeviceHashStateUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceHashStateStore _hashStates;

    public GetDeviceHashStateUseCase(IAuthorizationBoundary auth, IDeviceHashStateStore hashStates)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(hashStates);
        _auth = auth;
        _hashStates = hashStates;
    }

    public async Task<ApplicationResult<DeviceHashStateView>> ExecuteAsync(
        GetDeviceHashStateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        DeviceHashState? state = await _hashStates
            .GetAsync(new DeviceId(query.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (state is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device hash state '{query.DeviceId}' not found."));
        }

        return ApplicationResults.Ok(WorkflowViewMapper.ToView(state));
    }
}

/// <summary>Projects derived Node workflow status from persisted facts (M6-01).</summary>
public sealed class ProjectNodeWorkflowQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }
}

/// <summary>
/// Assembles honest NodeWorkflowFacts from inventory / ops / bindings and projects status.
/// Omits TopologyBlocked when no topology-finding store is available.
/// </summary>
public sealed class ProjectNodeWorkflowUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly IDeviceHashStateStore _hashStates;
    private readonly IConnectionProfileReadStore _connections;
    private readonly IOnboardingStore _onboarding;
    private readonly IDeploymentStore _deployments;
    private readonly IPolicyApprovalStore _approvals;

    public ProjectNodeWorkflowUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        IDeviceHashStateStore hashStates,
        IConnectionProfileReadStore connections,
        IOnboardingStore onboarding,
        IDeploymentStore deployments,
        IPolicyApprovalStore approvals)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(approvals);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _hashStates = hashStates;
        _connections = connections;
        _onboarding = onboarding;
        _deployments = deployments;
        _approvals = approvals;
    }

    public async Task<ApplicationResult<NodeWorkflowProjectionView>> ExecuteAsync(
        ProjectNodeWorkflowQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        NodeId nodeId = new(query.NodeId);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{query.NodeId}' not found."));
        }

        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        DeviceId[] deviceIds = devices.Select(static d => d.Id).ToArray();
        IReadOnlyList<DeviceHashState> hashStates = await _hashStates
            .ListByDeviceIdsAsync(deviceIds, cancellationToken)
            .ConfigureAwait(false);

        // Devices without a persisted row still participate with empty/unknown hashes (Incomplete / recovery via flags).
        Dictionary<Guid, DeviceHashState> byDevice = hashStates.ToDictionary(static s => s.DeviceId.Value);
        List<DeviceHashState> assembled = new(devices.Count);
        foreach (Device device in devices.OrderBy(static d => d.Id.Value))
        {
            if (byDevice.TryGetValue(device.Id.Value, out DeviceHashState? existing))
            {
                assembled.Add(existing);
            }
            else
            {
                // No persisted row yet: known-empty (not ambiguous) → Incomplete, not RecoveryRequired.
                assembled.Add(DeviceHashState.Create(
                    device.Id,
                    desiredPolicyHash: null,
                    desiredArtifactHash: null,
                    lastCommittedPolicyHash: null,
                    lastCommittedArtifactHash: null,
                    actualManagedResourceHash: null,
                    actualKnown: true,
                    anchorKnown: true,
                    updatedAtUtc: DateTimeOffset.UnixEpoch));
            }
        }

        ActiveEffectfulOperationKind activeOp = await ResolveActiveOperationAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<NodeWorkflowStatus> blockers = await BuildReadinessBlockersAsync(
            node,
            devices,
            cancellationToken).ConfigureAwait(false);

        bool recovery = node.ManagementState == ManagementState.RecoveryRequired
                        || devices.Any(static d => d.ManagementState == ManagementState.RecoveryRequired);

        NodeWorkflowFacts facts = new(recovery, activeOp, blockers, assembled);
        NodeWorkflowProjection projection = NodeWorkflowStatusProjector.Project(facts);
        return ApplicationResults.Ok(WorkflowViewMapper.ToView(projection));
    }

    private async Task<ActiveEffectfulOperationKind> ResolveActiveOperationAsync(
        NodeId nodeId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Domain.Onboarding.OnboardingOperation> onboardingOps = await _onboarding
            .ListNonterminalByNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        if (onboardingOps.Count > 0)
        {
            return ActiveEffectfulOperationKind.Onboarding;
        }

        IReadOnlyList<Domain.Deployment.DeploymentOperation> deploymentOps = await _deployments
            .ListNonterminalByNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        if (deploymentOps.Count > 0)
        {
            return ActiveEffectfulOperationKind.Deployment;
        }

        return ActiveEffectfulOperationKind.None;
    }

    private async Task<IReadOnlyList<NodeWorkflowStatus>> BuildReadinessBlockersAsync(
        Node node,
        IReadOnlyList<Device> devices,
        CancellationToken cancellationToken)
    {
        List<NodeWorkflowStatus> blockers = [];
        if (devices.Count == 0 || !node.SatisfiesActiveDeviceCardinality())
        {
            blockers.Add(NodeWorkflowStatus.InventoryIncomplete);
        }

        bool missingConnection = false;
        foreach (Device device in devices.Where(static d => d.Enabled))
        {
            ConnectionProfileReadModel? profile = await _connections
                .GetAsync(device.Id, cancellationToken)
                .ConfigureAwait(false);
            if (profile is null)
            {
                missingConnection = true;
                break;
            }
        }

        if (missingConnection)
        {
            blockers.Add(NodeWorkflowStatus.ConnectionInvalid);
        }

        if (devices.Any(static d => d.Enabled && d.LastCompletedCaptureId is null))
        {
            blockers.Add(NodeWorkflowStatus.CaptureRequired);
        }

        // TopologyBlocked omitted — no authoritative topology-finding store for M6-01.

        if (node.ManagementState == ManagementState.Unmanaged
            && devices.Any(static d => d.Enabled && d.ManagementState == ManagementState.Unmanaged))
        {
            blockers.Add(NodeWorkflowStatus.OnboardingRequired);
        }

        IReadOnlyList<PolicyDesiredBinding> companyBindings = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Company, scopeId: null, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PolicyDesiredBinding> nodeBindings = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Node, node.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (companyBindings.Count == 0 && nodeBindings.Count == 0)
        {
            blockers.Add(NodeWorkflowStatus.PolicyRequired);
        }

        return blockers;
    }
}

internal static class WorkflowViewMapper
{
    public static DeviceHashStateView ToView(DeviceHashState state)
    {
        DeviceSyncClassification classification = DeviceHashStateClassifier.Classify(state);
        return new DeviceHashStateView
        {
            DeviceId = state.DeviceId.Value,
            DesiredPolicyHashHex = state.DesiredPolicyHash?.ToString(),
            DesiredArtifactHashHex = state.DesiredArtifactHash?.ToString(),
            LastCommittedPolicyHashHex = state.LastCommittedPolicyHash?.ToString(),
            LastCommittedArtifactHashHex = state.LastCommittedArtifactHash?.ToString(),
            ActualManagedResourceHashHex = state.ActualManagedResourceHash?.ToString(),
            ActualKnown = state.ActualKnown,
            AnchorKnown = state.AnchorKnown,
            SyncClassification = classification,
            UpdatedAtUtc = state.UpdatedAtUtc,
            RowVersion = state.RowVersion,
        };
    }

    public static NodeWorkflowProjectionView ToView(NodeWorkflowProjection projection)
        => new()
        {
            NodeStatus = projection.NodeStatus,
            Devices = projection.Devices.Select(static d => new DeviceWorkflowProjectionView
            {
                DeviceId = d.DeviceId.Value,
                HashState = ToView(d.HashState),
                SyncClassification = d.SyncClassification,
                ContributingStatus = d.ContributingStatus,
            }).ToArray(),
        };
}
