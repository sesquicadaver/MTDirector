using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Drift;
using Mfc.Application.Models;
using Mfc.Domain.Workflow;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for global bounded drift polling (M6-03).</summary>
public sealed class PollManagedDriftJobResult
{
    public required IReadOnlyList<Guid> DeviceIdsPolled { get; init; }

    public required IReadOnlyList<Guid> DriftEventIds { get; init; }
}

/// <summary>
/// Invokes <see cref="DetectManagedDriftUseCase"/> for a bounded batch of devices with last_committed.
/// Uses one global poll configuration — no per-device schedules.
/// </summary>
public sealed class PollManagedDriftJobUseCase
{
    private readonly IDeviceHashStateStore _hashStates;
    private readonly DetectManagedDriftUseCase _detect;

    public PollManagedDriftJobUseCase(IDeviceHashStateStore hashStates, DetectManagedDriftUseCase detect)
    {
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(detect);
        _hashStates = hashStates;
        _detect = detect;
    }

    public async Task<ApplicationResult<PollManagedDriftJobResult>> ExecuteAsync(
        string actor,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (batchSize < 1)
        {
            return ApplicationResults.Fail(ApplicationError.Validation("batchSize must be >= 1."));
        }

        IReadOnlyList<DeviceHashState> states = await _hashStates
            .ListWithLastCommittedAsync(batchSize, cancellationToken)
            .ConfigureAwait(false);

        List<Guid> polled = [];
        List<Guid> events = [];
        foreach (DeviceHashState state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplicationResult<DriftEventView> result = await _detect.ExecuteAsync(
                new DetectManagedDriftCommand
                {
                    Actor = actor,
                    DeviceId = state.DeviceId.Value,
                    PersistActualHash = false,
                },
                cancellationToken).ConfigureAwait(false);
            polled.Add(state.DeviceId.Value);
            if (result.IsSuccess && result.Value is not null)
            {
                events.Add(result.Value.Id);
            }
        }

        return ApplicationResults.Ok(new PollManagedDriftJobResult
        {
            DeviceIdsPolled = polled,
            DriftEventIds = events,
        });
    }
}
