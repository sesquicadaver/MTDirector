using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Domain;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Onboarding;

public sealed class OnboardingPrerequisiteReportView
{
    public required Guid NodeId { get; init; }

    public required bool Passed { get; init; }

    public required IReadOnlyList<OnboardingFindingView> Findings { get; init; }
}

public sealed class OnboardingFindingView
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public Guid? DeviceId { get; init; }

    public string? Target { get; init; }
}

public sealed class OnboardingAnchorPlacementView
{
    public required string Marker { get; init; }

    public required string Chain { get; init; }

    public required string Family { get; init; }

    public required string Mode { get; init; }

    public required uint ExpectedOrdinal { get; init; }

    public required string BeforeLabel { get; init; }

    public required string AfterLabel { get; init; }
}

public sealed class OnboardingPlanSummaryView
{
    public required Guid PlanId { get; init; }

    public required Guid NodeId { get; init; }

    public required byte[] PlanHash { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required IReadOnlyList<OnboardingAnchorPlacementView> Placements { get; init; }
}

public sealed class OnboardingOperationSummaryView
{
    public required Guid OperationId { get; init; }

    public required Guid PlanId { get; init; }

    public required Guid NodeId { get; init; }

    public required OnboardingOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool NodeManaged { get; init; }
}

public sealed class OnboardingRecoveryStatusView
{
    public required Guid NodeId { get; init; }

    public Guid? OperationId { get; init; }

    public required OnboardingOperationState OperationState { get; init; }

    public string? ErrorCode { get; init; }

    public required string NodeManagementState { get; init; }

    public required OnboardingRecoveryAction Action { get; init; }

    public required IReadOnlyList<string> DeviceManagementStates { get; init; }
}

public sealed class ValidateOnboardingPrerequisitesCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public required IReadOnlyList<OnboardingDevicePrerequisiteFacts> Facts { get; init; }
}

public sealed class ValidateOnboardingPrerequisitesWorkflowUseCase
{
    public const string Operation = "onboarding.validate_prerequisites";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IAuditEventWriter _audit;

    public ValidateOnboardingPrerequisitesWorkflowUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _nodes = nodes;
        _audit = audit;
    }

    public async Task<ApplicationResult<OnboardingPrerequisiteReportView>> ExecuteAsync(
        ValidateOnboardingPrerequisitesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.OnboardingRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Node? node = await _nodes.GetAsync(new NodeId(command.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
        }

        Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> byDevice = command.Facts
            .ToDictionary(static f => f.DeviceId);
        OnboardingPrerequisiteResult result = ValidateOnboardingPrerequisitesUseCase.Execute(node, byDevice);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new { node_id = command.NodeId, passed = result.Passed }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(new OnboardingPrerequisiteReportView
        {
            NodeId = command.NodeId,
            Passed = result.Passed,
            Findings = result.Findings.Select(static f => new OnboardingFindingView
            {
                Code = f.Code,
                Severity = f.Severity,
                Message = f.Message,
                DeviceId = f.DeviceId?.Value,
                Target = f.Target,
            }).ToArray(),
        });
    }
}

public sealed class CreateOnboardingPlanCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid NodeId { get; init; }

    public required byte[] NodeMembershipHash { get; init; }

    public required byte[] TopologyProjectionHash { get; init; }

    public required IReadOnlyList<DeviceOnboardingPlan> DevicePlans { get; init; }
}

public sealed class CreateOnboardingPlanUseCase
{
    public const string Operation = "onboarding.create_plan";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IOnboardingStore _onboarding;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;

    public CreateOnboardingPlanUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IOnboardingStore onboarding,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _nodes = nodes;
        _onboarding = onboarding;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ApplicationResult<OnboardingPlanSummaryView>> ExecuteAsync(
        CreateOnboardingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.OnboardingWrite, cancellationToken).ConfigureAwait(false);
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
            membership = Convert.ToHexString(command.NodeMembershipHash),
            topology = Convert.ToHexString(command.TopologyProjectionHash),
            devices = command.DevicePlans.Select(static p => p.DeviceId.Value).ToArray(),
        });
        ApplicationResult<OnboardingPlanSummaryView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                OnboardingPlan? existing = await _onboarding.GetPlanAsync(new OnboardingPlanId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Onboarding plan '{id}' not found."))
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

            OnboardingPlan plan = OnboardingPlan.Create(
                node,
                Hash256.Create(command.NodeMembershipHash),
                Hash256.Create(command.TopologyProjectionHash),
                command.DevicePlans,
                new UserId(ActorKey.FromActor(command.Actor)),
                _clock.UtcNow);
            await _onboarding.AddPlanAsync(plan, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, plan.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new { plan_id = plan.Id.Value, node_id = plan.NodeId.Value }),
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

    internal static OnboardingPlanSummaryView ToPlanView(OnboardingPlan plan)
    {
        List<OnboardingAnchorPlacementView> placements = [];
        foreach (DeviceOnboardingPlan devicePlan in plan.DevicePlans)
        {
            foreach (AnchorPlacement placement in devicePlan.AnchorPlacements)
            {
                placements.Add(new OnboardingAnchorPlacementView
                {
                    Marker = placement.Key.Marker,
                    Chain = placement.Chain.ToString(),
                    Family = placement.Family.ToString(),
                    Mode = placement.Mode.ToString(),
                    ExpectedOrdinal = placement.ExpectedAnchorOrdinal,
                    BeforeLabel = placement.Mode == AnchorPlacementMode.Append ? string.Empty : "static-reference",
                    AfterLabel = placement.Mode == AnchorPlacementMode.Append ? "end-of-chain" : string.Empty,
                });
            }
        }

        return new OnboardingPlanSummaryView
        {
            PlanId = plan.Id.Value,
            NodeId = plan.NodeId.Value,
            PlanHash = plan.PlanHash.Bytes.ToArray(),
            ExpiresAtUtc = plan.ExpiresAtUtc,
            Placements = placements,
        };
    }
}

public sealed class StartOnboardingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid PlanId { get; init; }

    public required byte[] PlanHash { get; init; }
}

public sealed class StartOnboardingUseCase
{
    public const string Operation = "onboarding.start";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IOnboardingStore _onboarding;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly IOnboardingRuntime _runtime;

    public StartOnboardingUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IOnboardingStore onboarding,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock,
        IOnboardingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(runtime);
        _auth = auth;
        _nodes = nodes;
        _onboarding = onboarding;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
        _runtime = runtime;
    }

    public async Task<ApplicationResult<OnboardingOperationSummaryView>> ExecuteAsync(
        StartOnboardingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.OnboardingWrite, cancellationToken).ConfigureAwait(false);
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
                new ApplicationError(OnboardingCodes.PlanHashMismatch, "Start requires an exact 32-byte plan_hash."));
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.PlanId,
            hash = Convert.ToHexString(command.PlanHash),
        });
        ApplicationResult<OnboardingOperationSummaryView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                OnboardingOperation? existing = await _onboarding.GetOperationAsync(new OnboardingOperationId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Onboarding operation '{id}' not found."))
                    : ApplicationResults.Ok(ToOperationView(existing, timeline: [], nodeManaged: false));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            OnboardingPlan? plan = await _onboarding.GetPlanAsync(new OnboardingPlanId(command.PlanId), cancellationToken)
                .ConfigureAwait(false);
            if (plan is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Onboarding plan '{command.PlanId}' not found."));
            }

            if (!plan.PlanHash.Equals(Hash256.Create(command.PlanHash)))
            {
                return ApplicationResults.Fail(
                    new ApplicationError(OnboardingCodes.PlanHashMismatch, "plan_hash does not match the stored plan."));
            }

            Node? node = await _nodes.GetAsync(plan.NodeId, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{plan.NodeId.Value}' not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            IReadOnlyList<OnboardingOperation> existing = await _onboarding
                .ListNonterminalByNodeAsync(node.Id, cancellationToken)
                .ConfigureAwait(false);
            OnboardingOperationGate.EnsureCanStart(node, plan, existing, now);
            OnboardingOperation operation = OnboardingOperation.Create(plan, node, new UserId(ActorKey.FromActor(command.Actor)), now);
            await _onboarding.AddOperationAsync(operation, cancellationToken).ConfigureAwait(false);
            OnboardingExecutionResult executed;
            try
            {
                executed = await _runtime.ExecuteAsync(node, plan, operation, now, now, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                operation.EnsureTransition(OnboardingOperationState.RecoveryRequired, now, "failed");
                await _onboarding.SaveOperationAsync(operation, cancellationToken).ConfigureAwait(false);
                return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
            }

            await _onboarding.SaveOperationAsync(operation, cancellationToken).ConfigureAwait(false);
            await _nodes.UpdateAsync(node, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, operation.Id.Value, cancellationToken)
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
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ToOperationView(operation, executed.Timeline, executed.NodeManaged));
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

    internal static OnboardingOperationSummaryView ToOperationView(
        OnboardingOperation operation,
        IReadOnlyList<string> timeline,
        bool nodeManaged)
        => new()
        {
            OperationId = operation.Id.Value,
            PlanId = operation.PlanId.Value,
            NodeId = operation.NodeId.Value,
            State = operation.State,
            ErrorCode = operation.ErrorCode,
            Timeline = timeline,
            NodeManaged = nodeManaged,
        };
}

public sealed class RollbackOnboardingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid OperationId { get; init; }
}

public sealed class RollbackOnboardingWorkflowUseCase
{
    public const string Operation = "onboarding.rollback";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IOnboardingStore _onboarding;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly IOnboardingRuntime _runtime;

    public RollbackOnboardingWorkflowUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IOnboardingStore onboarding,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock,
        IOnboardingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(runtime);
        _auth = auth;
        _nodes = nodes;
        _onboarding = onboarding;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
        _runtime = runtime;
    }

    public async Task<ApplicationResult<OnboardingOperationSummaryView>> ExecuteAsync(
        RollbackOnboardingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.OnboardingWrite, cancellationToken).ConfigureAwait(false);
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
        ApplicationResult<OnboardingOperationSummaryView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                OnboardingOperation? existing = await _onboarding.GetOperationAsync(new OnboardingOperationId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Onboarding operation '{id}' not found."))
                    : ApplicationResults.Ok(StartOnboardingUseCase.ToOperationView(existing, [], nodeManaged: false));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            OnboardingOperation? operation = await _onboarding
                .GetOperationAsync(new OnboardingOperationId(command.OperationId), cancellationToken)
                .ConfigureAwait(false);
            if (operation is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Onboarding operation '{command.OperationId}' not found."));
            }

            OnboardingPlan? plan = await _onboarding.GetPlanAsync(operation.PlanId, cancellationToken).ConfigureAwait(false);
            if (plan is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Onboarding plan '{operation.PlanId.Value}' not found."));
            }

            Node? node = await _nodes.GetAsync(operation.NodeId, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{operation.NodeId.Value}' not found."));
            }

            OnboardingRollbackResult rolled = await _runtime.RollbackAsync(node, plan, operation, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            await _onboarding.SaveOperationAsync(operation, cancellationToken).ConfigureAwait(false);
            await _nodes.UpdateAsync(node, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, operation.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new
                {
                    operation_id = operation.Id.Value,
                    state = operation.State.ToString(),
                }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(StartOnboardingUseCase.ToOperationView(operation, rolled.Timeline, nodeManaged: false));
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

public sealed class GetOnboardingRecoveryStatusQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public Guid? OperationId { get; init; }

    public IReadOnlyList<ActualFilterRule> LiveAnchors { get; init; } = [];

    public OnboardingSystemNameFacts? WatchdogNames { get; init; }
}

public sealed class GetOnboardingRecoveryStatusUseCase
{
    public const string Operation = "onboarding.get_recovery_status";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IOnboardingStore _onboarding;
    private readonly IAuditEventWriter _audit;

    public GetOnboardingRecoveryStatusUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IOnboardingStore onboarding,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _nodes = nodes;
        _onboarding = onboarding;
        _audit = audit;
    }

    public async Task<ApplicationResult<OnboardingRecoveryStatusView>> ExecuteAsync(
        GetOnboardingRecoveryStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.OnboardingRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Node? node = await _nodes.GetAsync(new NodeId(query.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{query.NodeId}' not found."));
        }

        OnboardingOperation? operation = null;
        if (query.OperationId is Guid operationId)
        {
            operation = await _onboarding.GetOperationAsync(new OnboardingOperationId(operationId), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            IReadOnlyList<OnboardingOperation> nonterminal = await _onboarding
                .ListNonterminalByNodeAsync(node.Id, cancellationToken)
                .ConfigureAwait(false);
            operation = nonterminal.Count > 0 ? nonterminal[0] : null;
        }

        OnboardingRecoveryAction action = OnboardingRecoveryAction.CleanupRolledBack;
        OnboardingOperationState state = operation?.State ?? OnboardingOperationState.Created;
        if (operation is not null && query.LiveAnchors.Count > 0)
        {
            OnboardingPlan? plan = await _onboarding.GetPlanAsync(operation.PlanId, cancellationToken).ConfigureAwait(false);
            if (plan is not null && plan.DevicePlans.Count > 0)
            {
                OnboardingAnchorSetState anchors = OnboardingRecoveryDecision.ClassifyAnchors(
                    plan.DevicePlans[0].RequiredAnchorSet,
                    query.LiveAnchors,
                    operation.State == OnboardingOperationState.Committed);
                OnboardingWatchdogPresence watchdog = query.WatchdogNames is null
                    ? OnboardingWatchdogPresence.AbsentOrDisabled
                    : OnboardingRecoveryDecision.ClassifyWatchdog(query.WatchdogNames);
                action = OnboardingRecoveryDecision.Decide(
                    anchors,
                    watchdog,
                    operation.State == OnboardingOperationState.Committed);
            }
        }
        else if (operation?.State == OnboardingOperationState.RecoveryRequired)
        {
            action = OnboardingRecoveryAction.RecoveryRequired;
        }
        else if (operation?.State == OnboardingOperationState.Committed)
        {
            action = OnboardingRecoveryAction.KeepManaged;
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
        return ApplicationResults.Ok(new OnboardingRecoveryStatusView
        {
            NodeId = node.Id.Value,
            OperationId = operation?.Id.Value,
            OperationState = state,
            ErrorCode = operation?.ErrorCode,
            NodeManagementState = node.ManagementState.ToString(),
            Action = action,
            DeviceManagementStates = node.Devices.Select(static d => $"{d.Id.Value:D}:{d.ManagementState}").ToArray(),
        });
    }
}

/// <summary>Builds a CapabilityProfile for prerequisite RPC mapping (read-only facts, no RouterOS I/O).</summary>
public static class OnboardingPrerequisiteFactFactory
{
    public static CapabilityProfile CreateCapability(
        uint major,
        uint minor,
        uint patch,
        string channel,
        SupportState supportState)
        => CapabilityProfile.Create(
            RouterOsVersion.Create((int)major, (int)minor, (int)patch, string.IsNullOrWhiteSpace(channel) ? "stable" : channel),
            NonEmptyName.Create("x86_64"),
            NonEmptyName.Create("CHR"),
            packages: ["routeros"],
            ipv6Supported: true,
            vrrpSupported: true,
            bridgeSupported: true,
            apiSslCertificatePresent: true,
            supportState,
            Hash256.Create(new byte[Hash256.Size]));
}
