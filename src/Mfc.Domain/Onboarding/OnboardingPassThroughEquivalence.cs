using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>Pass-through semantic equivalence of pre/post filter snapshots (Onboarding Spec §41).</summary>
public enum OnboardingEquivalenceVerdict : byte
{
    Proven = 0,
    NotProven = 1,
    Indeterminate = 2,
}

/// <summary>Result of <see cref="OnboardingPassThroughEquivalence.Evaluate"/>.</summary>
public sealed class OnboardingEquivalenceResult
{
    public required OnboardingEquivalenceVerdict Verdict { get; init; }

    public required string? Code { get; init; }

    public required string Message { get; init; }

    public bool RequiresRollback
        => Verdict is OnboardingEquivalenceVerdict.NotProven or OnboardingEquivalenceVerdict.Indeterminate;
}

/// <summary>
/// Proves the only new packet-path insertion is jump → bootstrap return → original unmanaged successor.
/// Unknown matchers or unclassifiable additions are INDETERMINATE.
/// </summary>
public static class OnboardingPassThroughEquivalence
{
    public const string AnalyzerVersion = "mfc.onboarding.passthrough.v1";

    public static OnboardingEquivalenceResult Evaluate(
        IReadOnlyList<ActualFilterRule> before,
        IReadOnlyList<ActualFilterRule> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        foreach (ActualFilterRule rule in after)
        {
            if (rule.UnknownMatchers.Count > 0)
            {
                return Indeterminate("Post-bootstrap snapshot contains an unknown matcher.");
            }

            if (IsOnboardingOwned(rule))
            {
                continue;
            }

            if (string.Equals(rule.Action, "jump", StringComparison.OrdinalIgnoreCase)
                && OnboardingBootstrapWritePlanner.IsNormativeBootstrapRoot(rule.JumpTarget))
            {
                return Indeterminate("Unmanaged rule jumps into a bootstrap root.");
            }
        }

        foreach (ActualFilterRule rule in after.Where(IsOnboardingOwned))
        {
            if (OnboardingBootstrapWritePlanner.IsNormativeBootstrapReturn(rule.Comment))
            {
                if (!string.Equals(rule.Action, "return", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(rule.JumpTarget)
                    || rule.KnownMatchers.Count > 0
                    || rule.Disabled)
                {
                    return NotProven("Bootstrap return is not a single unconditional return.");
                }

                continue;
            }

            if (!string.Equals(rule.Action, "jump", StringComparison.OrdinalIgnoreCase)
                || rule.JumpTarget is null
                || !OnboardingBootstrapWritePlanner.IsNormativeBootstrapRoot(rule.JumpTarget)
                || rule.Disabled
                || rule.KnownMatchers.Count > 0)
            {
                return NotProven("Permanent anchor is not an enabled jump to the bootstrap root.");
            }
        }

        (string ChainKey, IReadOnlyList<string> Fingerprints)[] unmanagedBefore = UnmanagedSequences(before);
        (string ChainKey, IReadOnlyList<string> Fingerprints)[] unmanagedAfter = UnmanagedSequences(after);
        if (unmanagedBefore.Length != unmanagedAfter.Length)
        {
            return NotProven("Unmanaged chain set changed during onboarding.");
        }

        for (int i = 0; i < unmanagedBefore.Length; i++)
        {
            if (!string.Equals(unmanagedBefore[i].ChainKey, unmanagedAfter[i].ChainKey, StringComparison.Ordinal)
                || !unmanagedBefore[i].Fingerprints.SequenceEqual(unmanagedAfter[i].Fingerprints, StringComparer.Ordinal))
            {
                return NotProven("Unmanaged filter content or relative order changed.");
            }
        }

        bool extra = after.Any(static r => !IsOnboardingOwned(r) && !IsUnmanaged(r));
        if (extra)
        {
            return Indeterminate("Post-bootstrap snapshot contains an unclassifiable filter rule.");
        }

        return new OnboardingEquivalenceResult
        {
            Verdict = OnboardingEquivalenceVerdict.Proven,
            Code = null,
            Message = "Pass-through jump-to-return is the only new filter path.",
        };
    }

    public static bool IsOnboardingOwned(ActualFilterRule rule)
        => OnboardingBootstrapWritePlanner.IsNormativeBootstrapReturn(rule.Comment)
           || OnboardingBootstrapWritePlanner.IsNormativeAnchorMarker(rule.Comment)
           || OnboardingBootstrapWritePlanner.IsNormativeBootstrapRoot(rule.Chain);

    private static bool IsUnmanaged(ActualFilterRule rule) => !IsOnboardingOwned(rule);

    private static (string ChainKey, IReadOnlyList<string> Fingerprints)[] UnmanagedSequences(
        IReadOnlyList<ActualFilterRule> snapshot)
        => snapshot
            .Where(IsUnmanaged)
            .GroupBy(static r => $"{(int)r.Family}:{r.Chain}", StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static g => (
                g.Key,
                (IReadOnlyList<string>)g.OrderBy(static r => r.Ordinal)
                    .Select(static r => FilterRuleFingerprint.Compute(r).ToString())
                    .ToArray()))
            .ToArray();

    private static OnboardingEquivalenceResult NotProven(string message)
        => new()
        {
            Verdict = OnboardingEquivalenceVerdict.NotProven,
            Code = OnboardingCodes.BootstrapSemanticEquivalenceNotProven,
            Message = message,
        };

    private static OnboardingEquivalenceResult Indeterminate(string message)
        => new()
        {
            Verdict = OnboardingEquivalenceVerdict.Indeterminate,
            Code = OnboardingCodes.BootstrapSemanticEquivalenceNotProven,
            Message = message,
        };
}
