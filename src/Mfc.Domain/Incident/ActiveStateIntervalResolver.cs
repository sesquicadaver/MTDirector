using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Incident;

/// <summary>
/// Resolves the active historical state interval for a device at occurred_at (M7.3-02 / next-2 §4).
/// Controller derives state from its own deployment/audit timeline — never from external hashes.
/// </summary>
public static class ActiveStateIntervalResolver
{
    public const string AnalyzerVersion = "mfc.active-state-interval.v1";

    /// <summary>Resolves the interval covering <paramref name="query"/>.OccurredAt.</summary>
    public static ActiveStateIntervalResult Resolve(
        ActiveStateIntervalQuery query,
        ActiveStateTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);

        DateTimeOffset occurredAt = query.OccurredAt.ToUniversalTime();
        List<ActiveStateTransitionFact> deviceTransitions = snapshot.Transitions
            .Where(t => t.DeviceId.Equals(query.DeviceId))
            .ToList();

        if (deviceTransitions.Count == 0)
        {
            return new ActiveStateIntervalResult
            {
                Interval = null,
                Certainty = ActiveStateCertainty.Unknown,
                Findings =
                [
                    new ActiveStateIntervalFinding
                    {
                        Code = ActiveStateIntervalCodes.NoTimelineData,
                        Message = $"No active-state transitions exist for device '{query.DeviceId}'.",
                        Subject = query.DeviceId.ToString(),
                    },
                ],
            };
        }

        IReadOnlyList<ActiveStateInterval> intervals = ActiveStateIntervalBuilder.BuildIntervals(
            query.DeviceId,
            deviceTransitions);

        ActiveStateInterval? match = intervals.FirstOrDefault(i => i.Contains(occurredAt));
        if (match is not null)
        {
            return new ActiveStateIntervalResult
            {
                Interval = match,
                Certainty = match.Certainty,
                Findings =
                [
                    new ActiveStateIntervalFinding
                    {
                        Code = ActiveStateIntervalCodes.Resolved,
                        Message = "Historical active-state interval resolved.",
                        Subject = query.DeviceId.ToString(),
                    },
                ],
            };
        }

        ActiveStateInterval first = intervals[0];
        if (occurredAt < first.ValidFrom)
        {
            return new ActiveStateIntervalResult
            {
                Interval = null,
                Certainty = ActiveStateCertainty.Unknown,
                Findings =
                [
                    new ActiveStateIntervalFinding
                    {
                        Code = ActiveStateIntervalCodes.OccurredBeforeFirstTransition,
                        Message =
                            $"occurred_at '{occurredAt:O}' precedes first known transition '{first.ValidFrom:O}'.",
                        Subject = query.DeviceId.ToString(),
                    },
                ],
            };
        }

        return new ActiveStateIntervalResult
        {
            Interval = null,
            Certainty = ActiveStateCertainty.Unknown,
            Findings =
            [
                new ActiveStateIntervalFinding
                {
                    Code = ActiveStateIntervalCodes.NoTimelineData,
                    Message = $"occurred_at '{occurredAt:O}' is not covered by any built interval.",
                    Subject = query.DeviceId.ToString(),
                },
            ],
        };
    }
}
