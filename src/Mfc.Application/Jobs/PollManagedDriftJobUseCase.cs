using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Drift;
using Mfc.Application.Models;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for global bounded drift polling (M6-03).</summary>
public sealed class PollManagedDriftJobResult
{
    public required IReadOnlyList<Guid> DeviceIdsPolled { get; init; }

    public required IReadOnlyList<Guid> DriftEventIds { get; init; }
}

/// <summary>
/// Invokes live managed-state reads then <see cref="DetectManagedDriftUseCase"/> for a bounded batch
/// of devices with last_committed (AUDIT-DRIFT-01 / F12). Failed live reads are not NoDrift.
/// </summary>
public sealed class PollManagedDriftJobUseCase
{
    private readonly IDeviceHashStateStore _hashStates;
    private readonly IManagedDriftLiveReadPort _liveRead;
    private readonly DetectManagedDriftUseCase _detect;

    public PollManagedDriftJobUseCase(
        IDeviceHashStateStore hashStates,
        IManagedDriftLiveReadPort liveRead,
        DetectManagedDriftUseCase detect)
    {
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(liveRead);
        ArgumentNullException.ThrowIfNull(detect);
        _hashStates = hashStates;
        _liveRead = liveRead;
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
            if (state.LastCommittedArtifactHash is not Hash256 committed)
            {
                continue;
            }

            ManagedDriftLiveReadResult live = await _liveRead
                .ReadActualManagedResourceAsync(state.DeviceId, committed, cancellationToken)
                .ConfigureAwait(false);

            DetectManagedDriftCommand command = BuildDetectCommand(actor, state.DeviceId.Value, live);
            ApplicationResult<DriftEventView> result = await _detect.ExecuteAsync(command, cancellationToken)
                .ConfigureAwait(false);
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

    private static DetectManagedDriftCommand BuildDetectCommand(
        string actor,
        Guid deviceId,
        ManagedDriftLiveReadResult live)
    {
        if (live.Succeeded && live.ContentMatchedExpected
            && !string.IsNullOrWhiteSpace(live.ObservedManagedResourceHashHex))
        {
            return new DetectManagedDriftCommand
            {
                Actor = actor,
                DeviceId = deviceId,
                ActualManagedResourceHashHex = live.ObservedManagedResourceHashHex,
                PersistActualHash = true,
            };
        }

        string detail = live.Detail ?? live.ErrorCode ?? "live managed-state observation failed";
        return new DetectManagedDriftCommand
        {
            Actor = actor,
            DeviceId = deviceId,
            IgnorePersistedActual = true,
            PersistActualHash = false,
            Findings =
            [
                new DriftFindingInput
                {
                    Kind = DriftFindingKind.ManagedRuleChanged,
                    Detail = $"{live.ErrorCode ?? "live_read_failed"}: {detail}",
                },
            ],
        };
    }
}
