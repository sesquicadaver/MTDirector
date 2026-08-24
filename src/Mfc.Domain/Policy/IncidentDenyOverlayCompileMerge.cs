using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Merges active incident deny overlay rules into composed active rules for M3 compile (M7.4-03).
/// Does not alter the logical effective policy hash — overlays are compile-time only (next-2).
/// </summary>
public static class IncidentDenyOverlayCompileMerge
{
    public sealed class MergeResult
    {
        public bool IsSuccess { get; init; }

        public string? Code { get; init; }

        public string? Message { get; init; }

        public IReadOnlyList<PolicyRule> Rules { get; init; } = [];

        public int ActiveOverlayCount { get; init; }

        public static MergeResult Ok(IReadOnlyList<PolicyRule> rules, int activeOverlayCount)
            => new() { IsSuccess = true, Rules = rules, ActiveOverlayCount = activeOverlayCount };

        public static MergeResult Fail(string code, string message)
            => new() { IsSuccess = false, Code = code, Message = message };
    }

    /// <summary>
    /// Appends enabled overlay rules after composed rules, preserving pipeline stage order.
    /// Skips expired overlays; fails closed on invalid documents or UUID collisions.
    /// </summary>
    public static MergeResult Merge(
        IReadOnlyList<PolicyRule> composedActiveRules,
        IReadOnlyList<PolicyLayer> overlayLayers,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(composedActiveRules);
        ArgumentNullException.ThrowIfNull(overlayLayers);

        HashSet<Guid> ruleIds = composedActiveRules.Select(static r => r.Id.Value).ToHashSet();
        List<OverlayEntry> overlayEntries = [];
        int activeOverlayCount = 0;

        foreach (PolicyLayer layer in overlayLayers.OrderBy(static l => l.PolicyId))
        {
            if (layer.Kind != PolicyKind.IncidentDenyOverlay)
            {
                return MergeResult.Fail(
                    IncidentDenyOverlayCodes.WrongKind,
                    "Overlay layer must be INCIDENT_DENY_OVERLAY.");
            }

            IncidentDenyOverlayMetadata? metadata = layer.PolicyDocument.IncidentDenyOverlayMetadata;
            if (metadata is not null && metadata.IsExpired(nowUtc))
            {
                continue;
            }

            string guard = IncidentDenyOverlayDocumentGuard.Validate(layer.PolicyDocument);
            if (guard != IncidentDenyOverlayCodes.ValidDocument)
            {
                return MergeResult.Fail(guard, "Incident deny overlay document is invalid for compile.");
            }

            activeOverlayCount++;
            foreach (PolicyRule rule in layer.PolicyDocument.Rules.Where(static r => r.Enabled))
            {
                if (!ruleIds.Add(rule.Id.Value))
                {
                    return MergeResult.Fail(
                        IncidentDenyOverlayCodes.RuleUuidCollision,
                        $"Rule UUID '{rule.Id.Value:D}' collides with composed policy or another overlay.");
                }

                overlayEntries.Add(new OverlayEntry(layer.RevisionId, layer.PolicyId, rule));
            }
        }

        if (overlayEntries.Count == 0)
        {
            return MergeResult.Ok(composedActiveRules, activeOverlayCount);
        }

        List<OrderedRule> merged = composedActiveRules
            .Select(static r => new OrderedRule(Guid.Empty, Guid.Empty, r))
            .Concat(overlayEntries.Select(static e => new OrderedRule(e.RevisionId, e.PolicyId, e.Rule)))
            .OrderBy(static e => e.Rule.Family)
            .ThenBy(static e => e.Rule.Chain)
            .ThenBy(static e => PolicyPipelineV1.Ordinal(e.Rule.Stage))
            .ThenBy(static e => e.RevisionId)
            .ThenBy(static e => e.Rule.Ordinal)
            .ThenBy(static e => e.Rule.Id.Value)
            .ToList();

        return MergeResult.Ok(merged.Select(static e => e.Rule).ToArray(), activeOverlayCount);
    }

    private readonly record struct OverlayEntry(Guid RevisionId, Guid PolicyId, PolicyRule Rule);

    private readonly record struct OrderedRule(Guid RevisionId, Guid PolicyId, PolicyRule Rule);
}
