using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Deployment;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

/// <summary>Deploy one incident deny overlay on a Node via M3 compile + M4 deployment (M7.4-03).</summary>
public sealed class DeployIncidentDenyOverlayCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid OverlayPolicyId { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required byte[] CurrentDependencyFingerprint { get; init; }

    public required byte[] CurrentCapabilityHash { get; init; }

    public required Guid PlanIdempotencyKey { get; init; }

    public required Guid DeployIdempotencyKey { get; init; }

    public required byte[] LogicalPolicyHash { get; init; }

    public required byte[] AnalysisBundleHash { get; init; }

    public required byte[] TopologyProjectionHash { get; init; }

    public required IReadOnlyList<DeviceDeploymentPlan> DevicePlans { get; init; }

    public required IReadOnlyList<PacketPathPairFact> PacketPathPairs { get; init; }
}

/// <summary>Compile + plan + start outcome for one Node incident overlay reaction.</summary>
public sealed class DeployIncidentDenyOverlayUseCase
{
    public const string Operation = "incident.overlay.deploy";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IAuditEventWriter _audit;
    private readonly CompileNodeFilterArtifactsUseCase _compile;
    private readonly CreateDeploymentPlanUseCase _createPlan;
    private readonly StartDeploymentUseCase _startDeployment;
    private readonly EmitResponseFeedbackUseCase _feedback;

    public DeployIncidentDenyOverlayUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IAuditEventWriter audit,
        CompileNodeFilterArtifactsUseCase compile,
        CreateDeploymentPlanUseCase createPlan,
        StartDeploymentUseCase startDeployment,
        EmitResponseFeedbackUseCase feedback)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(compile);
        ArgumentNullException.ThrowIfNull(createPlan);
        ArgumentNullException.ThrowIfNull(startDeployment);
        ArgumentNullException.ThrowIfNull(feedback);
        _auth = auth;
        _policies = policies;
        _approvals = approvals;
        _audit = audit;
        _compile = compile;
        _createPlan = createPlan;
        _startDeployment = startDeployment;
        _feedback = feedback;
    }

    public async Task<ApplicationResult<DeployIncidentDenyOverlayView>> ExecuteAsync(
        DeployIncidentDenyOverlayCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentOverlayDeploy,
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
                "Deploy requires an INCIDENT_DENY_OVERLAY policy."));
        }

        if (policy.OwnerId != command.NodeId)
        {
            return ApplicationResults.Fail(new ApplicationError(
                IncidentDenyOverlayCodes.OverlayNodeMismatch,
                "Overlay policy owner_id must match the deploy target Node."));
        }

        IReadOnlyList<PolicyDesiredBinding> bindings = await _approvals
            .ListActiveBindingsAsync(
                PolicyBindingScope.IncidentDenyOverlay,
                command.NodeId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bindings.Any(b => b.PolicyId == policy.Id && b.State == PolicyBindingState.Active))
        {
            return ApplicationResults.Fail(new ApplicationError(
                IncidentDenyOverlayCodes.BindingRequired,
                "Incident deny overlay requires an ACTIVE desired binding before deploy."));
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

        PolicyDesiredBinding activeBinding = bindings.Single(b =>
            b.PolicyId == policy.Id && b.State == PolicyBindingState.Active);
        Guid? incidentId = await IncidentOverlayFeedbackSupport.TryResolveOverlayIncidentIdAsync(
            _policies,
            activeBinding,
            cancellationToken).ConfigureAwait(false);
        if (incidentId is not null)
        {
            Guid[] deviceIds = command.DevicePlans.Select(static p => p.DeviceId.Value).ToArray();
            await IncidentOverlayFeedbackSupport.EmitAsync(
                _feedback,
                command.Actor,
                ResponseFeedbackEventKind.Planned,
                incidentId.Value,
                command.NodeId,
                deviceIds,
                command.PlanIdempotencyKey,
                policyHash: command.LogicalPolicyHash,
                planHash: plan.Value!.PlanHash,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        ApplicationResult<DeploymentOperationSummaryView> started = await _startDeployment.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = command.Actor,
                IdempotencyKey = command.DeployIdempotencyKey,
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
                PacketPathPairs = command.PacketPathPairs,
            },
            cancellationToken).ConfigureAwait(false);
        if (started.IsFailure)
        {
            return ApplicationResults.Fail(started.Error!);
        }

        if (incidentId is not null)
        {
            Guid[] deviceIds = command.DevicePlans.Select(static p => p.DeviceId.Value).ToArray();
            await IncidentOverlayFeedbackSupport.EmitAsync(
                _feedback,
                command.Actor,
                ResponseFeedbackEventKind.Started,
                incidentId.Value,
                command.NodeId,
                deviceIds,
                command.DeployIdempotencyKey,
                policyHash: command.LogicalPolicyHash,
                planHash: plan.Value!.PlanHash,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await _audit.AppendAsync(
            command.Actor,
            Operation,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                node_id = command.NodeId,
                overlay_policy_id = policy.Id.Value,
                plan_id = plan.Value!.PlanId,
                operation_id = started.Value!.OperationId,
            }),
            cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(
            DeployIncidentDenyOverlayView.FromParts(
                policy.Id.Value,
                compiled.Value!,
                plan.Value,
                started.Value!));
    }
}
