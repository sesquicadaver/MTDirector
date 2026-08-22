using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Incident;

/// <summary>Builds non-overlapping <see cref="ActiveStateInterval"/> rows from timeline facts (M7.3-02).</summary>
public static class ActiveStateIntervalBuilder
{
    /// <summary>Materializes ordered intervals for one device from scripted transition facts.</summary>
    public static IReadOnlyList<ActiveStateInterval> BuildIntervals(
        DeviceId deviceId,
        IReadOnlyList<ActiveStateTransitionFact> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        if (transitions.Count == 0)
        {
            return [];
        }

        ActiveStateTransitionFact[] ordered = transitions
            .OrderBy(static t => t.EffectiveAt)
            .ThenBy(static t => t.DeviceId.Value)
            .ToArray();

        ValidateTimeline(deviceId, ordered);

        ActiveStateInterval[] intervals = new ActiveStateInterval[ordered.Length];
        for (int i = 0; i < ordered.Length; i++)
        {
            ActiveStateTransitionFact transition = ordered[i];
            DateTimeOffset? validUntil = i < ordered.Length - 1
                ? ordered[i + 1].EffectiveAt
                : null;
            intervals[i] = new ActiveStateInterval(
                deviceId,
                transition.EffectiveAt,
                validUntil,
                transition.PolicyHash,
                transition.ArtifactHash,
                transition.ConfigurationHash,
                transition.TopologyHash,
                ActiveStateIntervalClassifier.Classify(transition));
        }

        return intervals;
    }

    private static void ValidateTimeline(DeviceId deviceId, ActiveStateTransitionFact[] ordered)
    {
        DateTimeOffset? previousAt = null;
        foreach (ActiveStateTransitionFact transition in ordered)
        {
            if (!transition.DeviceId.Equals(deviceId))
            {
                throw new DomainInvariantException(
                    $"{ActiveStateIntervalCodes.DeviceMismatch}: transition device_id does not match timeline device.");
            }

            DateTimeOffset effectiveAt = transition.EffectiveAt.ToUniversalTime();
            if (previousAt is DateTimeOffset prior)
            {
                if (effectiveAt == prior)
                {
                    throw new DomainInvariantException(
                        $"{ActiveStateIntervalCodes.DuplicateTransitionInstant}: duplicate effective_at on device timeline.");
                }

                if (effectiveAt < prior)
                {
                    throw new DomainInvariantException(
                        $"{ActiveStateIntervalCodes.NonMonotonicTimeline}: transitions must be strictly increasing.");
                }
            }

            previousAt = effectiveAt;
        }
    }
}

/// <summary>Derives certainty for one transition fact.</summary>
public static class ActiveStateIntervalClassifier
{
    public static ActiveStateCertainty Classify(ActiveStateTransitionFact transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        bool hasPolicy = transition.PolicyHash is not null;
        bool hasArtifact = transition.ArtifactHash is not null;
        bool hasConfiguration = transition.ConfigurationHash is not null;
        bool hasTopology = transition.TopologyHash is not null;
        if (!hasPolicy && !hasArtifact && !hasConfiguration && !hasTopology)
        {
            return ActiveStateCertainty.Unknown;
        }

        if (hasPolicy
            && hasArtifact
            && hasConfiguration
            && hasTopology
            && transition.ActualKnown
            && transition.AnchorKnown)
        {
            return ActiveStateCertainty.Proven;
        }

        return ActiveStateCertainty.Partial;
    }
}
