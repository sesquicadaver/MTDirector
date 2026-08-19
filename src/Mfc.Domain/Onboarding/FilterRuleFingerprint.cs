using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Content-addresses one actual filter rule without RouterOS <c>.id</c> or live ordinal
/// (Onboarding Spec §20 / M5-04 AC#3 / AC#8). Occurrence rank is computed separately.
/// </summary>
public static class FilterRuleFingerprint
{
    public const string Prefix = "mfc.onboarding.filter_rule.v1";

    /// <summary>SHA-256 of family, chain, flags, action, jump, comment, and ordered matchers.</summary>
    public static Hash256 Compute(ActualFilterRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, Prefix);
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)rule.Family]);
        AppendUtf8(hasher, rule.Chain);
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)(rule.Disabled ? 1 : 0)]);
        hasher.AppendData([(byte)(rule.Dynamic ? 1 : 0)]);
        AppendUtf8(hasher, rule.Action ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, rule.JumpTarget ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, rule.Comment ?? string.Empty);
        hasher.AppendData([(byte)0]);
        foreach (KeyValuePair<string, string> pair in rule.KnownMatchers
                     .Concat(rule.UnknownMatchers)
                     .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
                     .ThenBy(static kv => kv.Value, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, pair.Key);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, pair.Value);
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// Zero-based occurrence of <paramref name="fingerprint"/> among ordered rules on the same family+chain.
    /// </summary>
    public static uint OccurrenceRank(
        IReadOnlyList<ActualFilterRule> chainRules,
        ActualFilterRule target,
        Hash256 fingerprint)
    {
        ArgumentNullException.ThrowIfNull(chainRules);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(fingerprint);
        uint rank = 0;
        foreach (ActualFilterRule rule in chainRules)
        {
            if (!Compute(rule).Equals(fingerprint))
            {
                continue;
            }

            if (ReferenceEquals(rule, target) || (rule.Ordinal == target.Ordinal && rule.Family == target.Family
                && string.Equals(rule.Chain, target.Chain, StringComparison.OrdinalIgnoreCase)))
            {
                return rank;
            }

            rank++;
        }

        throw new DomainInvariantException("Target rule is not present in the chain snapshot.");
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
