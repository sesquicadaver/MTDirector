using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Policy;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for expired-exception reconciliation (M6-03). Zero RouterOS writes.</summary>
public sealed class ReconcileExpiredExceptionBindingsJobResult
{
    public required IReadOnlyList<Guid> ExpiredBindingIds { get; init; }
}

/// <summary>
/// Transitions due EXCEPTION bindings to EXPIRED_PENDING_RECONCILIATION via
/// <see cref="ExpireExceptionBindingUseCase"/>. Does not write RouterOS.
/// </summary>
public sealed class ReconcileExpiredExceptionBindingsJobUseCase
{
    private readonly IPolicyApprovalStore _approvals;
    private readonly ExpireExceptionBindingUseCase _expire;
    private readonly Abstractions.Time.IClock _clock;

    public ReconcileExpiredExceptionBindingsJobUseCase(
        IPolicyApprovalStore approvals,
        ExpireExceptionBindingUseCase expire,
        Abstractions.Time.IClock clock)
    {
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(expire);
        ArgumentNullException.ThrowIfNull(clock);
        _approvals = approvals;
        _expire = expire;
        _clock = clock;
    }

    public async Task<ApplicationResult<ReconcileExpiredExceptionBindingsJobResult>> ExecuteAsync(
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
            .ListDueExceptionBindingsAsync(_clock.UtcNow, batchSize, cancellationToken)
            .ConfigureAwait(false);

        List<Guid> expired = [];
        foreach (PolicyDesiredBinding binding in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplicationResult<PolicyBindingView> result = await _expire.ExecuteAsync(
                new ExpireExceptionBindingCommand
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

        return ApplicationResults.Ok(new ReconcileExpiredExceptionBindingsJobResult
        {
            ExpiredBindingIds = expired,
        });
    }
}
