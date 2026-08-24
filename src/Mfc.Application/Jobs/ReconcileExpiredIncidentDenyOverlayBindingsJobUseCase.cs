using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain.Policy;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for expired incident overlay reconciliation (M7.4-04). Zero RouterOS writes.</summary>
public sealed class ReconcileExpiredIncidentDenyOverlayBindingsJobResult
{
    public required IReadOnlyList<Guid> ExpiredBindingIds { get; init; }
}

/// <summary>
/// Transitions due INCIDENT_DENY_OVERLAY bindings to EXPIRED_PENDING_RECONCILIATION via
/// <see cref="ExpireIncidentDenyOverlayBindingUseCase"/>. Does not write RouterOS or create plans.
/// </summary>
public sealed class ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase
{
    private readonly IPolicyApprovalStore _approvals;
    private readonly ExpireIncidentDenyOverlayBindingUseCase _expire;
    private readonly Abstractions.Time.IClock _clock;

    public ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase(
        IPolicyApprovalStore approvals,
        ExpireIncidentDenyOverlayBindingUseCase expire,
        Abstractions.Time.IClock clock)
    {
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(expire);
        ArgumentNullException.ThrowIfNull(clock);
        _approvals = approvals;
        _expire = expire;
        _clock = clock;
    }

    public async Task<ApplicationResult<ReconcileExpiredIncidentDenyOverlayBindingsJobResult>> ExecuteAsync(
        string actor,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (batchSize < 1)
        {
            return ApplicationResults.Fail(ApplicationError.Validation("batchSize must be >= 1."));
        }

        IReadOnlyList<PolicyDesiredBinding> due = await _approvals
            .ListDueIncidentDenyOverlayBindingsAsync(_clock.UtcNow, batchSize, cancellationToken)
            .ConfigureAwait(false);

        List<Guid> expired = [];
        foreach (PolicyDesiredBinding binding in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplicationResult<PolicyBindingView> result = await _expire.ExecuteAsync(
                new ExpireIncidentDenyOverlayBindingCommand
                {
                    Actor = actor,
                    IdempotencyKey = Guid.NewGuid(),
                    BindingId = binding.Id.Value,
                    ExpectedRowVersion = binding.RowVersion,
                },
                cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                expired.Add(binding.Id.Value);
            }
        }

        return ApplicationResults.Ok(new ReconcileExpiredIncidentDenyOverlayBindingsJobResult
        {
            ExpiredBindingIds = expired,
        });
    }
}
