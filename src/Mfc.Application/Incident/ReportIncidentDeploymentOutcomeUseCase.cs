using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Models;
using Mfc.Domain.Deployment;
using Mfc.Domain.Incident;

namespace Mfc.Application.Incident;

public sealed class ReportIncidentDeploymentOutcomeCommand
{
    public required string Actor { get; init; }

    public required Guid IncidentId { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid CorrelationId { get; init; }

    public IReadOnlyList<Guid> DeviceIds { get; init; } = [];

    public required StandaloneDeploymentResult Result { get; init; }

    public byte[]? PolicyHash { get; init; }

    public byte[]? ArtifactHash { get; init; }

    public byte[]? PlanHash { get; init; }

    public string? ResidualRisk { get; init; }
}

/// <summary>Maps standalone deployment outcomes to RESPONSE_APPLIED/VERIFIED/ROLLED_BACK/RECOVERY_REQUIRED (M7.4-06).</summary>
public sealed class ReportIncidentDeploymentOutcomeUseCase
{
    public const string Operation = "incident.response.report_deployment_outcome";

    private readonly EmitResponseFeedbackUseCase _feedback;

    public ReportIncidentDeploymentOutcomeUseCase(EmitResponseFeedbackUseCase feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        _feedback = feedback;
    }

    public async Task<ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>>> ExecuteAsync(
        ReportIncidentDeploymentOutcomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Result);

        List<ResponseFeedbackEventView> emitted = [];
        StandaloneDeploymentResult result = command.Result;

        if (result.Succeeded && result.State == DeploymentOperationState.Committed)
        {
            ApplicationResult<ResponseFeedbackEventView> applied = await EmitAsync(
                command,
                ResponseFeedbackEventKind.Applied,
                verificationResults: "committed",
                rollbackStatus: null,
                cancellationToken).ConfigureAwait(false);
            if (applied.IsFailure)
            {
                return ApplicationResults.Fail(applied.Error!);
            }

            emitted.Add(applied.Value!);

            ApplicationResult<ResponseFeedbackEventView> verified = await EmitAsync(
                command,
                ResponseFeedbackEventKind.Verified,
                verificationResults: "activation_verified",
                rollbackStatus: "none",
                cancellationToken).ConfigureAwait(false);
            if (verified.IsFailure)
            {
                return ApplicationResults.Fail(verified.Error!);
            }

            emitted.Add(verified.Value!);
            return ApplicationResults.Ok<IReadOnlyList<ResponseFeedbackEventView>>(emitted);
        }

        if (result.State == DeploymentOperationState.RolledBack)
        {
            ApplicationResult<ResponseFeedbackEventView> rolledBack = await EmitAsync(
                command,
                ResponseFeedbackEventKind.RolledBack,
                verificationResults: "rollback_completed",
                rollbackStatus: result.ErrorCode ?? "rolled_back",
                cancellationToken).ConfigureAwait(false);
            if (rolledBack.IsFailure)
            {
                return ApplicationResults.Fail(rolledBack.Error!);
            }

            emitted.Add(rolledBack.Value!);
            return ApplicationResults.Ok<IReadOnlyList<ResponseFeedbackEventView>>(emitted);
        }

        ApplicationResult<ResponseFeedbackEventView> recovery = await EmitAsync(
            command,
            ResponseFeedbackEventKind.RecoveryRequired,
            verificationResults: result.ErrorCode,
            rollbackStatus: result.DetachedArtifactPreservedOnFailure ? "artifact_preserved" : "artifact_lost",
            cancellationToken).ConfigureAwait(false);
        if (recovery.IsFailure)
        {
            return ApplicationResults.Fail(recovery.Error!);
        }

        emitted.Add(recovery.Value!);
        return ApplicationResults.Ok<IReadOnlyList<ResponseFeedbackEventView>>(emitted);
    }

    private Task<ApplicationResult<ResponseFeedbackEventView>> EmitAsync(
        ReportIncidentDeploymentOutcomeCommand command,
        ResponseFeedbackEventKind kind,
        string? verificationResults,
        string? rollbackStatus,
        CancellationToken cancellationToken)
        => _feedback.ExecuteAsync(
            new EmitResponseFeedbackCommand
            {
                Actor = command.Actor,
                Kind = kind,
                IncidentId = command.IncidentId,
                NodeId = command.NodeId,
                DeviceIds = command.DeviceIds,
                CorrelationId = command.CorrelationId,
                PolicyHash = command.PolicyHash,
                ArtifactHash = command.ArtifactHash,
                PlanHash = command.PlanHash,
                VerificationResults = verificationResults,
                RollbackStatus = rollbackStatus,
                ResidualRisk = command.ResidualRisk,
            },
            cancellationToken);
}
