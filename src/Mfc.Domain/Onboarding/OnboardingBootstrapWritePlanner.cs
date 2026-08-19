using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Builds the closed bootstrap write sequence from a device plan and live filter snapshot
/// (Onboarding Spec §23 / §27 / M5-05). Does not execute RouterOS commands.
/// </summary>
public static class OnboardingBootstrapWritePlanner
{
    public const string AnalyzerVersion = "mfc.onboarding.bootstrap_write.v1";

    /// <summary>
    /// Plans add-bootstrap-return then add-disabled-anchor for every required placement.
    /// Namespace collisions fail closed and yield no writes.
    /// </summary>
    public static OnboardingBootstrapWritePlan Plan(
        DeviceOnboardingPlan devicePlan,
        IReadOnlyList<ActualFilterRule> snapshot)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(snapshot);

        List<OnboardingBootstrapWriteFinding> findings = [];
        if (!devicePlan.BootstrapArtifactHash.Equals(BootstrapArtifact.Hash)
            || !string.Equals(BootstrapArtifact.ArtifactId, "8e40b9d4d67d42d6", StringComparison.Ordinal)
            || !BootstrapArtifact.ComputeSeedHash().Equals(BootstrapArtifact.Hash))
        {
            findings.Add(Blocker(
                OnboardingCodes.UnexpectedAnchorTarget,
                "Bootstrap artifact ID/hash does not match Spec §23.",
                "artifact"));
        }

        HashSet<string> rootNames = new(StringComparer.Ordinal);
        HashSet<string> markers = new(StringComparer.Ordinal);
        foreach (AnchorPlacement placement in devicePlan.AnchorPlacements)
        {
            rootNames.Add(BootstrapArtifact.RootChainName(placement.Family, placement.Chain));
            markers.Add(new AnchorKey(placement.Family, placement.Chain).Marker);
        }

        foreach (ActualFilterRule rule in snapshot)
        {
            bool rootHit = rootNames.Contains(rule.Chain)
                           || IsNormativeBootstrapRoot(rule.Chain);
            if (rootHit)
            {
                findings.Add(Blocker(
                    OnboardingCodes.BootstrapRootCollision,
                    $"Bootstrap root chain '{rule.Chain}' already exists.",
                    rule.Chain));
                findings.Add(Blocker(
                    OnboardingCodes.MfcNamespaceCollision,
                    $"MFC namespace collision on chain '{rule.Chain}'.",
                    rule.Chain));
            }

            if (IsNormativeAnchorMarker(rule.Comment) || IsNormativeBootstrapReturn(rule.Comment)
                || (rule.Comment is not null && markers.Contains(rule.Comment.Trim()))
                || (ActualFilterMarker.TryReadMarker(rule.Comment, out string? marker)
                    && marker is not null
                    && markers.Contains(marker)))
            {
                findings.Add(Blocker(
                    OnboardingCodes.AnchorMarkerCollision,
                    $"Permanent MFC onboarding marker '{rule.Comment}' already exists.",
                    "marker"));
                findings.Add(Blocker(
                    OnboardingCodes.MfcNamespaceCollision,
                    "MFC namespace collision on an onboarding marker.",
                    "marker"));
            }

            if (!string.IsNullOrWhiteSpace(rule.JumpTarget)
                && (rootNames.Contains(rule.JumpTarget) || IsNormativeBootstrapRoot(rule.JumpTarget)))
            {
                findings.Add(Blocker(
                    OnboardingCodes.MfcNamespaceCollision,
                    $"Existing rule jumps to bootstrap root '{rule.JumpTarget}'.",
                    rule.JumpTarget));
            }
        }

        if (findings.Count > 0)
        {
            return Finish(findings, []);
        }

        List<OnboardingBootstrapWrite> writes = [];
        foreach (AnchorPlacement placement in devicePlan.AnchorPlacements)
        {
            OnboardingBootstrapWrite ret = OnboardingBootstrapWrite.AddBootstrapReturn(placement.Family, placement.Chain);
            AssertSingleUnconditionalReturn(ret);
            writes.Add(ret);
            writes.Add(OnboardingBootstrapWrite.AddDisabledAnchor(placement));
        }

        return Finish(findings, writes);
    }

    public static void AssertSingleUnconditionalReturn(OnboardingBootstrapWrite write)
    {
        if (write.Kind != OnboardingBootstrapWriteKind.AddBootstrapReturn)
        {
            throw new DomainInvariantException("Expected a bootstrap-return add.");
        }

        if (write.Attributes.Count != 4
            || write.Attributes.Any(static a => a.Key is "jump-target" or "log" or ".id" or "src-address"))
        {
            throw new DomainInvariantException("Bootstrap root must contain exactly one unconditional return.");
        }
    }

    internal static bool IsNormativeBootstrapRoot(string? chain)
        => chain is not null
           && (chain.StartsWith("mfc4.", StringComparison.Ordinal)
               || chain.StartsWith("mfc6.", StringComparison.Ordinal))
           && chain.Contains(".r.", StringComparison.Ordinal)
           && chain.EndsWith(BootstrapArtifact.ArtifactId, StringComparison.Ordinal);

    internal static bool IsNormativeBootstrapReturn(string? comment)
        => comment is not null
           && comment.StartsWith(BootstrapArtifact.ReturnComment, StringComparison.Ordinal);

    internal static bool IsNormativeAnchorMarker(string? comment)
        => ActualFilterMarker.TryReadMarker(comment, out string? marker)
           && marker is not null
           && marker.StartsWith("mfc:anchor:v1:", StringComparison.Ordinal);

    private static OnboardingBootstrapWriteFinding Blocker(string code, string message, string? target)
        => new()
        {
            Code = code,
            Severity = OnboardingCodes.SeverityBlocker,
            Message = message,
            Target = target,
        };

    private static OnboardingBootstrapWritePlan Finish(
        List<OnboardingBootstrapWriteFinding> findings,
        List<OnboardingBootstrapWrite> writes)
    {
        IReadOnlyList<OnboardingBootstrapWriteFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Message, f.Target))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();
        return new OnboardingBootstrapWritePlan
        {
            Findings = ordered,
            Writes = writes,
        };
    }
}
