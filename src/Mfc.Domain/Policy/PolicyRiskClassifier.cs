namespace Mfc.Domain.Policy;

/// <summary>Normative risk mapping (Policy Model §60.2). Final risk is the maximum of all drivers.</summary>
public static class PolicyRiskClassifier
{
    private static readonly Dictionary<string, int> Rank = new(StringComparer.Ordinal)
    {
        [PolicyEvidenceAnalysisCodes.RiskNone] = 0,
        [PolicyEvidenceAnalysisCodes.RiskLow] = 1,
        [PolicyEvidenceAnalysisCodes.RiskMedium] = 2,
        [PolicyEvidenceAnalysisCodes.RiskHigh] = 3,
        [PolicyEvidenceAnalysisCodes.RiskCritical] = 4,
    };

    public static PolicyRiskResult Classify(
        PolicyRevisionDiffResult diff,
        IReadOnlyList<PolicyEvidenceFinding> findings,
        PolicyEvidenceSignals signals,
        IReadOnlyList<PolicyRule> before,
        IReadOnlyList<PolicyRule> after)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        List<string> drivers = [];
        string level = PolicyEvidenceAnalysisCodes.RiskNone;
        void Raise(string risk, string driver)
        {
            if (Rank[risk] > Rank[level])
            {
                level = risk;
            }

            if (!drivers.Contains(driver, StringComparer.Ordinal))
            {
                drivers.Add(driver);
            }
        }

        if (findings.Any(static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityBlocker))
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskCritical, "blocker");
        }

        if (signals.ManagementPathChanged)
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskCritical, PolicyEvidenceAnalysisCodes.ClassControlPlane);
        }

        if (signals.ExceptionChanged)
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskHigh, PolicyEvidenceAnalysisCodes.ClassException);
        }

        if (signals.DefaultDispositionChanged)
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskCritical, PolicyEvidenceAnalysisCodes.ClassDefaultDisposition);
        }

        if (signals.ZoneBindingChanged)
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskCritical, PolicyEvidenceAnalysisCodes.ClassZoneBinding);
        }

        if (HasFastTrackChange(before, after, diff))
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskHigh, PolicyEvidenceAnalysisCodes.ClassFastTrack);
        }

        foreach (string semantic in diff.SemanticClasses)
        {
            Raise(MapSemantic(semantic), semantic);
        }

        foreach (PolicyRuleDiffEntry entry in diff.RuleChanges)
        {
            Raise(MapRuleChange(entry, before, after), string.Join(',', entry.Changes));
        }

        if (diff.ObjectImpacts.Count > 0)
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskHigh, "object-impact");
        }

        if (diff.PacketSpaceClasses.Contains(PolicyEvidenceAnalysisCodes.PacketNewlyAccepted, StringComparer.Ordinal))
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskHigh, PolicyEvidenceAnalysisCodes.PacketNewlyAccepted);
        }

        if (diff.PacketSpaceClasses.Contains(PolicyEvidenceAnalysisCodes.PacketNewlyDenied, StringComparer.Ordinal))
        {
            Raise(PolicyEvidenceAnalysisCodes.RiskMedium, PolicyEvidenceAnalysisCodes.PacketNewlyDenied);
        }

        if (drivers.Count == 0)
        {
            drivers.Add(PolicyEvidenceAnalysisCodes.ClassNoEffectiveChange);
        }

        return new PolicyRiskResult
        {
            Level = level,
            Drivers = drivers.OrderBy(static d => d, StringComparer.Ordinal).ToArray(),
        };
    }

    private static bool HasFastTrackChange(
        IReadOnlyList<PolicyRule> before,
        IReadOnlyList<PolicyRule> after,
        PolicyRevisionDiffResult diff)
    {
        HashSet<Guid> changed = diff.RuleChanges.Select(static e => e.RuleId.Value).ToHashSet();
        return before.Concat(after).Any(r =>
            r.Effect.Kind == PolicyRuleEffect.FasttrackAccept && changed.Contains(r.Id.Value));
    }

    private static string MapSemantic(string semantic)
        => semantic switch
        {
            PolicyEvidenceAnalysisCodes.ClassNoEffectiveChange => PolicyEvidenceAnalysisCodes.RiskNone,
            PolicyEvidenceAnalysisCodes.ClassRestrictive => PolicyEvidenceAnalysisCodes.RiskMedium,
            PolicyEvidenceAnalysisCodes.ClassPermissive => PolicyEvidenceAnalysisCodes.RiskHigh,
            PolicyEvidenceAnalysisCodes.ClassMixed => PolicyEvidenceAnalysisCodes.RiskHigh,
            PolicyEvidenceAnalysisCodes.ClassFastTrack => PolicyEvidenceAnalysisCodes.RiskHigh,
            PolicyEvidenceAnalysisCodes.ClassException => PolicyEvidenceAnalysisCodes.RiskHigh,
            PolicyEvidenceAnalysisCodes.ClassControlPlane => PolicyEvidenceAnalysisCodes.RiskCritical,
            PolicyEvidenceAnalysisCodes.ClassDefaultDisposition => PolicyEvidenceAnalysisCodes.RiskCritical,
            PolicyEvidenceAnalysisCodes.ClassZoneBinding => PolicyEvidenceAnalysisCodes.RiskCritical,
            _ => PolicyEvidenceAnalysisCodes.RiskCritical,
        };

    private static string MapRuleChange(
        PolicyRuleDiffEntry entry,
        IReadOnlyList<PolicyRule> before,
        IReadOnlyList<PolicyRule> after)
    {
        PolicyRule? left = before.FirstOrDefault(r => r.Id == entry.RuleId);
        PolicyRule? right = after.FirstOrDefault(r => r.Id == entry.RuleId);
        bool addedAllow = right is not null
                          && left is null
                          && right.Effect.Kind is PolicyRuleEffect.Accept or PolicyRuleEffect.FasttrackAccept;
        bool removedAllow = left is not null
                            && right is null
                            && left.Effect.Kind is PolicyRuleEffect.Accept or PolicyRuleEffect.FasttrackAccept;
        bool addedDeny = right is not null
                         && left is null
                         && right.Effect.Kind is PolicyRuleEffect.Drop or PolicyRuleEffect.Reject;
        bool removedDeny = left is not null
                           && right is null
                           && left.Effect.Kind is PolicyRuleEffect.Drop or PolicyRuleEffect.Reject;
        if (addedAllow || removedDeny)
        {
            return PolicyEvidenceAnalysisCodes.RiskHigh;
        }

        if (addedDeny || removedAllow)
        {
            return PolicyEvidenceAnalysisCodes.RiskMedium;
        }

        if (entry.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeDisabled, StringComparer.Ordinal)
            && right is { Enabled: false })
        {
            return PolicyEvidenceAnalysisCodes.RiskLow;
        }

        return PolicyEvidenceAnalysisCodes.RiskLow;
    }
}
