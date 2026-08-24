using Mfc.Application.Common;
using Mfc.Application.Policies;
using Mfc.Domain.Incident;
using Mfc.Domain.Policy;

namespace Mfc.Application.Incident;

internal static class IncidentOverlayFeedbackSupport
{
    internal static async Task EmitAsync(
        EmitResponseFeedbackUseCase emit,
        string actor,
        ResponseFeedbackEventKind kind,
        Guid incidentId,
        Guid nodeId,
        IReadOnlyList<Guid> deviceIds,
        Guid correlationId,
        byte[]? policyHash = null,
        byte[]? artifactHash = null,
        byte[]? planHash = null,
        string? verificationResults = null,
        string? rollbackStatus = null,
        string? residualRisk = null,
        CancellationToken cancellationToken = default)
    {
        await emit.ExecuteAsync(
            new EmitResponseFeedbackCommand
            {
                Actor = actor,
                Kind = kind,
                IncidentId = incidentId,
                NodeId = nodeId,
                DeviceIds = deviceIds,
                CorrelationId = correlationId,
                PolicyHash = policyHash,
                ArtifactHash = artifactHash,
                PlanHash = planHash,
                VerificationResults = verificationResults,
                RollbackStatus = rollbackStatus,
                ResidualRisk = residualRisk,
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<Guid?> TryResolveOverlayIncidentIdAsync(
        Abstractions.Persistence.IPolicyStore policies,
        PolicyDesiredBinding binding,
        CancellationToken cancellationToken)
    {
        PolicyRevision? revision = await policies
            .GetRevisionAsync(binding.DesiredRevisionId, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            return null;
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision);
        return document.IsSuccess
            ? document.Value!.IncidentDenyOverlayMetadata?.IncidentId.Value
            : null;
    }
}
