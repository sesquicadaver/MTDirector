using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

/// <summary>Expire one INCIDENT_DENY_OVERLAY binding past valid_until without RouterOS write (M7.4-04).</summary>
public sealed class ExpireIncidentDenyOverlayBindingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid BindingId { get; init; }

    public required ulong ExpectedRowVersion { get; init; }
}

/// <summary>INCIDENT_DENY_OVERLAY TTL expiry → EXPIRED_PENDING_RECONCILIATION. No RouterOS write.</summary>
public sealed class ExpireIncidentDenyOverlayBindingUseCase
{
    public const string Operation = "incident.overlay.expire_binding";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;

    public ExpireIncidentDenyOverlayBindingUseCase(
        IAuthorizationBoundary auth,
        IPolicyApprovalStore approvals,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _approvals = approvals;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ApplicationResult<PolicyBindingView>> ExecuteAsync(
        ExpireIncidentDenyOverlayBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentOverlayRemove,
            cancellationToken).ConfigureAwait(false);
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
            command.BindingId,
            command.ExpectedRowVersion,
        });
        ApplicationResult<PolicyBindingView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (bindingId, ct) =>
            {
                PolicyDesiredBinding? existing = await _approvals
                    .GetBindingAsync(new PolicyBindingId(bindingId), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Policy binding '{bindingId}' not found."))
                    : ApplicationResults.Ok(ActivateDesiredBindingUseCase.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        PolicyDesiredBinding? binding = await _approvals
            .GetBindingAsync(new PolicyBindingId(command.BindingId), cancellationToken)
            .ConfigureAwait(false);
        if (binding is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy binding '{command.BindingId}' not found."));
        }

        if (binding.RowVersion != command.ExpectedRowVersion)
        {
            return ApplicationResults.Fail(ApplicationError.Conflict(
                "Policy binding row_version mismatch (expected_row_version CAS)."));
        }

        PolicyBindingEvaluation evaluation = PolicyBindingGate.EvaluateIncidentOverlayExpiry(binding, _clock.UtcNow);
        if (!evaluation.Allowed)
        {
            return ApplicationResults.Fail(new ApplicationError(
                evaluation.ErrorCode ?? PolicyApprovalCodes.BindingNotDue,
                evaluation.ErrorMessage ?? "Incident overlay expiry rejected."));
        }

        try
        {
            binding.ExpirePendingRemoval(_clock.UtcNow);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        await _approvals.SaveBindingAsync(binding, cancellationToken).ConfigureAwait(false);
        await _idempotency.SaveAsync(
            command.Actor, Operation, command.IdempotencyKey, requestHash, binding.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                binding_id = binding.Id.Value,
                state = binding.State.ToString(),
                deployment_started = false,
            }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ActivateDesiredBindingUseCase.ToView(binding));
    }
}

/// <summary>Mandatory removal plan for expired incident overlay (compile without overlay + plan, no start).</summary>
public sealed class PlanIncidentDenyOverlayRemovalCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid OverlayPolicyId { get; init; }

    public required Guid BindingId { get; init; }

    public required ulong ExpectedBindingRowVersion { get; init; }

    public required Guid ExpireIdempotencyKey { get; init; }

    public required Guid PlanIdempotencyKey { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required byte[] CurrentDependencyFingerprint { get; init; }

    public required byte[] CurrentCapabilityHash { get; init; }

    public required byte[] LogicalPolicyHash { get; init; }

    public required byte[] AnalysisBundleHash { get; init; }

    public required byte[] TopologyProjectionHash { get; init; }

    public required IReadOnlyList<DeviceDeploymentPlan> DevicePlans { get; init; }
}

/// <summary>
/// Creates a mandatory removal deployment plan after TTL expiry without starting RouterOS write (M7.4-04).
/// </summary>
public sealed class PlanIncidentDenyOverlayRemovalUseCase
{
    public const string Operation = "incident.overlay.plan_removal";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IAuditEventWriter _audit;
    private readonly ExpireIncidentDenyOverlayBindingUseCase _expire;
    private readonly CompileNodeFilterArtifactsUseCase _compile;
    private readonly CreateDeploymentPlanUseCase _createPlan;

    public PlanIncidentDenyOverlayRemovalUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IAuditEventWriter audit,
        ExpireIncidentDenyOverlayBindingUseCase expire,
        CompileNodeFilterArtifactsUseCase compile,
        CreateDeploymentPlanUseCase createPlan)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(expire);
        ArgumentNullException.ThrowIfNull(compile);
        ArgumentNullException.ThrowIfNull(createPlan);
        _auth = auth;
        _policies = policies;
        _approvals = approvals;
        _audit = audit;
        _expire = expire;
        _compile = compile;
        _createPlan = createPlan;
    }

    public async Task<ApplicationResult<PlanIncidentDenyOverlayRemovalView>> ExecuteAsync(
        PlanIncidentDenyOverlayRemovalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentOverlayRemove,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Policy? policy = await _policies
            .GetPolicyAsync(new PolicyId(command.OverlayPolicyId), cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Policy '{command.OverlayPolicyId}' was not found."));
        }

        if (policy.Kind != PolicyKind.IncidentDenyOverlay)
        {
            return ApplicationResults.Fail(new ApplicationError(
                IncidentDenyOverlayCodes.WrongKind,
                "Removal plan requires an INCIDENT_DENY_OVERLAY policy."));
        }

        if (policy.OwnerId != command.NodeId)
        {
            return ApplicationResults.Fail(new ApplicationError(
                IncidentDenyOverlayCodes.OverlayNodeMismatch,
                "Overlay policy owner_id must match the removal target Node."));
        }

        PolicyDesiredBinding? binding = await _approvals
            .GetBindingAsync(new PolicyBindingId(command.BindingId), cancellationToken)
            .ConfigureAwait(false);
        if (binding is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy binding '{command.BindingId}' not found."));
        }

        if (binding.Scope != PolicyBindingScope.IncidentDenyOverlay || binding.PolicyId != policy.Id)
        {
            return ApplicationResults.Fail(new ApplicationError(
                IncidentDenyOverlayCodes.BindingRequired,
                "Removal plan requires the incident deny overlay desired binding."));
        }

        if (binding.State == PolicyBindingState.Active)
        {
            ApplicationResult<PolicyBindingView> expired = await _expire.ExecuteAsync(
                new ExpireIncidentDenyOverlayBindingCommand
                {
                    Actor = command.Actor,
                    IdempotencyKey = command.ExpireIdempotencyKey,
                    BindingId = command.BindingId,
                    ExpectedRowVersion = command.ExpectedBindingRowVersion,
                },
                cancellationToken).ConfigureAwait(false);
            if (expired.IsFailure)
            {
                return ApplicationResults.Fail(expired.Error!);
            }
        }
        else if (binding.State != PolicyBindingState.ExpiredPendingReconciliation)
        {
            return ApplicationResults.Fail(new ApplicationError(
                IncidentDenyOverlayCodes.RemovalPlanRequired,
                "Removal plan requires an expired incident deny overlay binding."));
        }

        ApplicationResult<CompileNodeFilterArtifactsView> compiled = await _compile.ExecuteAsync(
            new CompileNodeFilterArtifactsCommand
            {
                Actor = command.Actor,
                NodeId = command.NodeId,
                AnalysisRunId = command.AnalysisRunId,
                CurrentDependencyFingerprint = command.CurrentDependencyFingerprint,
                CurrentCapabilityHash = command.CurrentCapabilityHash,
            },
            cancellationToken).ConfigureAwait(false);
        if (compiled.IsFailure)
        {
            return ApplicationResults.Fail(compiled.Error!);
        }

        ApplicationResult<DeploymentPlanSummaryView> plan = await _createPlan.ExecuteAsync(
            new CreateDeploymentPlanCommand
            {
                Actor = command.Actor,
                IdempotencyKey = command.PlanIdempotencyKey,
                NodeId = command.NodeId,
                LogicalPolicyHash = command.LogicalPolicyHash,
                AnalysisBundleHash = command.AnalysisBundleHash,
                TopologyProjectionHash = command.TopologyProjectionHash,
                DevicePlans = command.DevicePlans,
            },
            cancellationToken).ConfigureAwait(false);
        if (plan.IsFailure)
        {
            return ApplicationResults.Fail(plan.Error!);
        }

        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                node_id = command.NodeId,
                overlay_policy_id = policy.Id.Value,
                binding_id = command.BindingId,
                plan_id = plan.Value!.PlanId,
                deployment_started = false,
            }),
            cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(
            PlanIncidentDenyOverlayRemovalView.FromParts(
                policy.Id.Value,
                compiled.Value!,
                plan.Value));
    }
}
