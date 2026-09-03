using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Topology;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Deployment;

/// <summary>Workflow execute outcome returned by <see cref="IDeploymentRuntime"/> (M4-12).</summary>
public sealed class DeploymentWorkflowExecutionResult
{
    public required bool Succeeded { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool ActivationStarted { get; init; }
}

/// <summary>Workflow rollback outcome returned by <see cref="IDeploymentRuntime"/> (M4-12).</summary>
public sealed class DeploymentWorkflowRollbackResult
{
    public required bool Succeeded { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }
}

/// <summary>Workflow recovery outcome returned by <see cref="IDeploymentRuntime"/> (M4-12).</summary>
public sealed class DeploymentWorkflowRecoveryResult
{
    public required DeploymentRecoveryAction Action { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }
}

public sealed class DeploymentProbeView
{
    public required DeploymentProbeKind Kind { get; init; }

    public required string Destination { get; init; }

    public required int TimeoutMilliseconds { get; init; }

    public string? SourceAddress { get; init; }

    public string? RoutingTable { get; init; }

    public string? Interface { get; init; }
}

public sealed class DeploymentDevicePlanView
{
    public required Guid DeviceId { get; init; }

    public required byte[] OldArtifactHash { get; init; }

    public required byte[] NewArtifactHash { get; init; }

    public required IReadOnlyList<string> ActivationOrderMarkers { get; init; }

    public required IReadOnlyList<string> RollbackOrderMarkers { get; init; }

    public required uint WatchdogTtlSeconds { get; init; }

    public required IReadOnlyList<DeploymentProbeView> Probes { get; init; }
}

/// <summary>Existing device artifact-hash facts. Not a SemanticDiffEngine result.</summary>
public enum DeploymentSemanticDiffKind : byte
{
    Unspecified = 0,
    ArtifactUnchanged = 1,
    ArtifactChanged = 2,
}

public sealed class DeploymentSemanticDiffEntryView
{
    public required DeploymentSemanticDiffKind Kind { get; init; }

    public required string Path { get; init; }

    public Guid? DeviceId { get; init; }

    public required string Before { get; init; }

    public required string After { get; init; }

    public required string HashDelta { get; init; }
}

public sealed class DeploymentPlanSummaryView
{
    public required Guid PlanId { get; init; }

    public required Guid NodeId { get; init; }

    public required byte[] PlanHash { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required IReadOnlyList<string> SemanticDiffEntries { get; init; }

    public required IReadOnlyList<DeploymentSemanticDiffEntryView> SemanticDiff { get; init; }

    public required IReadOnlyList<Guid> ActivationOrderDeviceIds { get; init; }

    public required IReadOnlyList<Guid> RollbackOrderDeviceIds { get; init; }

    public required IReadOnlyList<DeploymentDevicePlanView> Devices { get; init; }
}

public sealed class DeploymentOperationSummaryView
{
    public required Guid OperationId { get; init; }

    public required Guid PlanId { get; init; }

    public required Guid NodeId { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }
}

public sealed class DeploymentRecoveryStatusView
{
    public required Guid NodeId { get; init; }

    public Guid? OperationId { get; init; }

    public required DeploymentOperationState OperationState { get; init; }

    public string? ErrorCode { get; init; }

    public required DeploymentRecoveryAction Action { get; init; }

    public required IReadOnlyList<string> DeviceStates { get; init; }
}

public sealed class CreateDeploymentPlanCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid NodeId { get; init; }

    public required byte[] LogicalPolicyHash { get; init; }

    public required byte[] AnalysisBundleHash { get; init; }

    public required byte[] TopologyProjectionHash { get; init; }

    public required IReadOnlyList<DeviceDeploymentPlan> DevicePlans { get; init; }
}

public sealed class CreateDeploymentPlanUseCase
{
    public const string Operation = "deployment.create_plan";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeploymentStore _deployments;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly VrrpPairConsistencyLoader _vrrpPair;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDeploymentPlanUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeploymentStore deployments,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock,
        VrrpPairConsistencyLoader vrrpPair,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(vrrpPair);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _nodes = nodes;
        _deployments = deployments;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
        _vrrpPair = vrrpPair;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<DeploymentPlanSummaryView>> ExecuteAsync(
        CreateDeploymentPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DeploymentWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.NodeId,
            policy = Convert.ToHexString(command.LogicalPolicyHash),
            analysis = Convert.ToHexString(command.AnalysisBundleHash),
            topology = Convert.ToHexString(command.TopologyProjectionHash),
            devices = command.DevicePlans.Select(static p => p.DeviceId.Value).ToArray(),
        });
        ApplicationResult<DeploymentPlanSummaryView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                DeploymentPlan? existing = await _deployments.GetPlanAsync(new DeploymentPlanId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Deployment plan '{id}' not found."))
                    : ApplicationResults.Ok(ToPlanView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            Node? node = await _nodes.GetAsync(new NodeId(command.NodeId), cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
            }

            ApplicationError? pairError = await VrrpPairPlanGate
                .BlockIfFailedAsync(_vrrpPair, node, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (pairError is not null)
            {
                return ApplicationResults.Fail(pairError);
            }

            DeploymentPlan plan = DeploymentPlan.Create(
                node,
                Hash256.Create(command.LogicalPolicyHash),
                Hash256.Create(command.AnalysisBundleHash),
                Hash256.Create(command.TopologyProjectionHash),
                command.DevicePlans,
                new UserId(ActorKey.FromActor(command.Actor)),
                _clock.UtcNow);
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _deployments.AddPlanAsync(plan, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                            command.Actor, Operation, command.IdempotencyKey, requestHash, plan.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new { plan_id = plan.Id.Value, node_id = plan.NodeId.Value }),
                            ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ToPlanView(plan));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }

    internal static DeploymentPlanSummaryView ToPlanView(DeploymentPlan plan)
    {
        List<string> semanticDiffEntries = [];
        List<DeploymentSemanticDiffEntryView> semanticDiff = [];
        List<DeploymentDevicePlanView> devices = [];
        foreach (DeviceDeploymentPlan device in plan.DevicePlans)
        {
            string oldHex = Convert.ToHexString(device.OldArtifactHash.Bytes);
            string newHex = Convert.ToHexString(device.NewArtifactHash.Bytes);
            bool unchanged = string.Equals(oldHex, newHex, StringComparison.Ordinal);
            string hashDelta = unchanged
                ? $"device:{device.DeviceId.Value:D}:artifact=unchanged"
                : $"device:{device.DeviceId.Value:D}:artifact {oldHex[..12]}… → {newHex[..12]}…";
            semanticDiffEntries.Add(hashDelta);
            semanticDiff.Add(new DeploymentSemanticDiffEntryView
            {
                Kind = unchanged
                    ? DeploymentSemanticDiffKind.ArtifactUnchanged
                    : DeploymentSemanticDiffKind.ArtifactChanged,
                Path = $"device/{device.DeviceId.Value:D}/artifact",
                DeviceId = device.DeviceId.Value,
                Before = oldHex,
                After = newHex,
                HashDelta = hashDelta,
            });
            devices.Add(new DeploymentDevicePlanView
            {
                DeviceId = device.DeviceId.Value,
                OldArtifactHash = device.OldArtifactHash.Bytes.ToArray(),
                NewArtifactHash = device.NewArtifactHash.Bytes.ToArray(),
                ActivationOrderMarkers = device.AnchorActivationOrder.Select(static k => k.Marker).ToArray(),
                RollbackOrderMarkers = device.AnchorRollbackOrder.Select(static k => k.Marker).ToArray(),
                WatchdogTtlSeconds = (uint)device.RollbackTtl.TotalSeconds,
                Probes = device.Probes.Select(static p => new DeploymentProbeView
                {
                    Kind = p.Kind,
                    Destination = p.Destination,
                    TimeoutMilliseconds = p.TimeoutMilliseconds,
                    SourceAddress = p.SourceAddress,
                    RoutingTable = p.RoutingTable,
                    Interface = p.Interface,
                }).ToArray(),
            });
        }

        return new DeploymentPlanSummaryView
        {
            PlanId = plan.Id.Value,
            NodeId = plan.NodeId.Value,
            PlanHash = plan.PlanHash.Bytes.ToArray(),
            ExpiresAtUtc = plan.ExpiresAtUtc,
            SemanticDiffEntries = semanticDiffEntries,
            SemanticDiff = semanticDiff,
            ActivationOrderDeviceIds = plan.ActivationOrder.Select(static d => d.Value).ToArray(),
            RollbackOrderDeviceIds = plan.RollbackOrder.Select(static d => d.Value).ToArray(),
            Devices = devices,
        };
    }
}

public sealed class StartDeploymentCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid PlanId { get; init; }

    public required byte[] PlanHash { get; init; }

    public required IReadOnlyList<PacketPathPairFact> PacketPathPairs { get; init; }
}

public sealed class StartDeploymentUseCase
{
    public const string Operation = "deployment.start";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeploymentStore _deployments;
    private readonly IDriftEventStore _driftEvents;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly IDeploymentRuntime _runtime;
    private readonly IUnitOfWork _unitOfWork;

    public StartDeploymentUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeploymentStore deployments,
        IDriftEventStore driftEvents,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock,
        IDeploymentRuntime runtime,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(driftEvents);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _nodes = nodes;
        _deployments = deployments;
        _driftEvents = driftEvents;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
        _runtime = runtime;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<DeploymentOperationSummaryView>> ExecuteAsync(
        StartDeploymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DeploymentWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        if (command.PlanHash is not { Length: Hash256.Size })
        {
            return ApplicationResults.Fail(
                new ApplicationError(DeploymentCodes.PlanHashMismatch, "Start requires an exact 32-byte plan_hash."));
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.PlanId,
            hash = Convert.ToHexString(command.PlanHash),
        });
        ApplicationResult<DeploymentOperationSummaryView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                DeploymentOperation? existing = await _deployments.GetOperationAsync(new DeploymentOperationId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Deployment operation '{id}' not found."))
                    : ApplicationResults.Ok(ToOperationView(existing, timeline: []));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            DeploymentPlan? plan = await _deployments.GetPlanAsync(new DeploymentPlanId(command.PlanId), cancellationToken)
                .ConfigureAwait(false);
            if (plan is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Deployment plan '{command.PlanId}' not found."));
            }

            if (!plan.PlanHash.Equals(Hash256.Create(command.PlanHash)))
            {
                return ApplicationResults.Fail(
                    new ApplicationError(DeploymentCodes.PlanHashMismatch, "plan_hash does not match the stored plan."));
            }

            Node? node = await _nodes.GetAsync(plan.NodeId, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{plan.NodeId.Value}' not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            IReadOnlyList<DeploymentOperation> existing = await _deployments
                .ListNonterminalByNodeAsync(node.Id, cancellationToken)
                .ConfigureAwait(false);
            bool hasBlockingCriticalDrift = await _driftEvents
                .HasBlockingCriticalDriftAsync(node.Id, cancellationToken)
                .ConfigureAwait(false);
            DeploymentOperationGate.EnsureCanStart(
                node, plan, existing, now, command.PacketPathPairs, hasBlockingCriticalDrift);
            DeploymentOperation operation = DeploymentOperation.Create(
                plan, node, new UserId(ActorKey.FromActor(command.Actor)), now);
            await _deployments.AddOperationAsync(operation, cancellationToken).ConfigureAwait(false);

            DeploymentWorkflowExecutionResult executed;
            try
            {
                executed = await _runtime.ExecuteAsync(
                        node, plan, operation, command.PacketPathPairs, now, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // AC#8: cancel after activation becomes controller rollback.
                if (ActivationStarted(operation.State))
                {
                    DeploymentWorkflowRollbackResult rolled = await _runtime
                        .RollbackAsync(node, plan, operation, now, CancellationToken.None)
                        .ConfigureAwait(false);
                    await _unitOfWork.ExecuteAsync(
                        async ct =>
                        {
                            await _deployments.SaveOperationAsync(operation, ct).ConfigureAwait(false);
                            await _idempotency.SaveAsync(
                                    command.Actor,
                                    Operation,
                                    command.IdempotencyKey,
                                    requestHash,
                                    operation.Id.Value,
                                    ct).ConfigureAwait(false);
                            await _audit.AppendAsync(
                                    command.Actor,
                                    Operation,
                                    JsonSerializer.Serialize(new
                                    {
                                        operation_id = operation.Id.Value,
                                        plan_id = plan.Id.Value,
                                        state = operation.State.ToString(),
                                        canceled_after_activation = true,
                                    }),
                                    ct).ConfigureAwait(false);
                        },
                        CancellationToken.None).ConfigureAwait(false);
                    return ApplicationResults.Ok(ToOperationView(operation, rolled.Timeline));
                }

                throw;
            }
            catch (InvalidOperationException ex)
            {
                if (!DeploymentOperation.IsTerminalState(operation.State)
                    && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
                {
                    operation.EnsureTransition(DeploymentOperationState.RecoveryRequired, now, "failed");
                }

                await _deployments.SaveOperationAsync(operation, cancellationToken).ConfigureAwait(false);
                return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
            }

            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _deployments.SaveOperationAsync(operation, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                            command.Actor, Operation, command.IdempotencyKey, requestHash, operation.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new
                            {
                                operation_id = operation.Id.Value,
                                plan_id = plan.Id.Value,
                                state = operation.State.ToString(),
                            }),
                            ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ToOperationView(operation, executed.Timeline));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }

    internal static bool ActivationStarted(DeploymentOperationState state)
        => state is DeploymentOperationState.Activating
            or DeploymentOperationState.Verifying
            or DeploymentOperationState.DisarmingWatchdog
            or DeploymentOperationState.RollbackPending
            or DeploymentOperationState.RollingBack;

    internal static DeploymentOperationSummaryView ToOperationView(
        DeploymentOperation operation,
        IReadOnlyList<string> timeline)
        => new()
        {
            OperationId = operation.Id.Value,
            PlanId = operation.PlanId.Value,
            NodeId = operation.NodeId.Value,
            State = operation.State,
            ErrorCode = operation.ErrorCode,
            Timeline = timeline,
        };
}

public sealed class RollbackDeploymentCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid OperationId { get; init; }
}

public sealed class RollbackDeploymentWorkflowUseCase
{
    public const string Operation = "deployment.rollback";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeploymentStore _deployments;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly IDeploymentRuntime _runtime;
    private readonly IUnitOfWork _unitOfWork;

    public RollbackDeploymentWorkflowUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeploymentStore deployments,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock,
        IDeploymentRuntime runtime,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _nodes = nodes;
        _deployments = deployments;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
        _runtime = runtime;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<DeploymentOperationSummaryView>> ExecuteAsync(
        RollbackDeploymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DeploymentWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new { command.OperationId });
        ApplicationResult<DeploymentOperationSummaryView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                DeploymentOperation? existing = await _deployments.GetOperationAsync(new DeploymentOperationId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Deployment operation '{id}' not found."))
                    : ApplicationResults.Ok(StartDeploymentUseCase.ToOperationView(existing, []));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            DeploymentOperation? operation = await _deployments
                .GetOperationAsync(new DeploymentOperationId(command.OperationId), cancellationToken)
                .ConfigureAwait(false);
            if (operation is null)
            {
                return ApplicationResults.Fail(
                    ApplicationError.NotFound($"Deployment operation '{command.OperationId}' not found."));
            }

            DeploymentPlan? plan = await _deployments.GetPlanAsync(operation.PlanId, cancellationToken).ConfigureAwait(false);
            if (plan is null)
            {
                return ApplicationResults.Fail(
                    ApplicationError.NotFound($"Deployment plan '{operation.PlanId.Value}' not found."));
            }

            Node? node = await _nodes.GetAsync(operation.NodeId, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{operation.NodeId.Value}' not found."));
            }

            DeploymentWorkflowRollbackResult rolled = await _runtime
                .RollbackAsync(node, plan, operation, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _deployments.SaveOperationAsync(operation, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                            command.Actor, Operation, command.IdempotencyKey, requestHash, operation.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new
                            {
                                operation_id = operation.Id.Value,
                                state = operation.State.ToString(),
                            }),
                            ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(StartDeploymentUseCase.ToOperationView(operation, rolled.Timeline));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
    }
}

public sealed class GetDeploymentRecoveryStatusQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public Guid? OperationId { get; init; }

    public IReadOnlyDictionary<string, string> LiveJumpsByMarker { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<(string Name, bool Disabled)> WatchdogSchedulers { get; init; } = [];
}

public sealed class GetDeploymentRecoveryStatusUseCase
{
    public const string Operation = "deployment.get_recovery_status";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeploymentStore _deployments;
    private readonly IAuditEventWriter _audit;

    public GetDeploymentRecoveryStatusUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeploymentStore deployments,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _nodes = nodes;
        _deployments = deployments;
        _audit = audit;
    }

    public async Task<ApplicationResult<DeploymentRecoveryStatusView>> ExecuteAsync(
        GetDeploymentRecoveryStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.DeploymentRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Node? node = await _nodes.GetAsync(new NodeId(query.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{query.NodeId}' not found."));
        }

        DeploymentOperation? operation = null;
        if (query.OperationId is Guid operationId)
        {
            operation = await _deployments.GetOperationAsync(new DeploymentOperationId(operationId), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            IReadOnlyList<DeploymentOperation> nonterminal = await _deployments
                .ListNonterminalByNodeAsync(node.Id, cancellationToken)
                .ConfigureAwait(false);
            operation = nonterminal.Count > 0 ? nonterminal[0] : null;
        }

        DeploymentRecoveryAction action = DeploymentRecoveryAction.MarkFailedOrCanceled;
        DeploymentOperationState state = operation?.State ?? DeploymentOperationState.Created;
        if (operation is not null && query.LiveJumpsByMarker.Count > 0)
        {
            DeploymentPlan? plan = await _deployments.GetPlanAsync(operation.PlanId, cancellationToken).ConfigureAwait(false);
            if (plan is not null && plan.DevicePlans.Count > 0)
            {
                DeviceDeploymentPlan device = plan.DevicePlans[0];
                DeploymentAnchorSetState anchors = DeploymentRecoveryDecision.ClassifyAnchors(
                    device.OldAnchorTargets,
                    device.NewAnchorTargets,
                    query.LiveJumpsByMarker);
                DeploymentWatchdogPresence watchdog = ClassifyWatchdog(query.WatchdogSchedulers);
                bool activationStarted = StartDeploymentUseCase.ActivationStarted(operation.State)
                    || operation.State is DeploymentOperationState.Committed
                    or DeploymentOperationState.RolledBack
                    or DeploymentOperationState.RecoveryRequired;
                action = DeploymentRecoveryDecision.Decide(
                    anchors,
                    watchdog,
                    committed: operation.State == DeploymentOperationState.Committed,
                    activationStarted: activationStarted);
            }
        }
        else if (operation?.State == DeploymentOperationState.RecoveryRequired)
        {
            action = DeploymentRecoveryAction.RecoveryRequired;
        }
        else if (operation?.State == DeploymentOperationState.Committed)
        {
            action = DeploymentRecoveryAction.KeepCommitted;
        }

        await _audit.AppendAsync(
            query.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                node_id = node.Id.Value,
                operation_id = operation?.Id.Value,
                action = action.ToString(),
            }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(new DeploymentRecoveryStatusView
        {
            NodeId = node.Id.Value,
            OperationId = operation?.Id.Value,
            OperationState = state,
            ErrorCode = operation?.ErrorCode,
            Action = action,
            DeviceStates = node.Devices.Select(static d => $"{d.Id.Value:D}:{d.ManagementState}").ToArray(),
        });
    }

    private static DeploymentWatchdogPresence ClassifyWatchdog(IReadOnlyList<(string Name, bool Disabled)> schedulers)
    {
        if (schedulers.Count == 0 || schedulers.All(static s => s.Disabled))
        {
            return DeploymentWatchdogPresence.AbsentOrDisabled;
        }

        return DeploymentWatchdogPresence.Active;
    }
}
