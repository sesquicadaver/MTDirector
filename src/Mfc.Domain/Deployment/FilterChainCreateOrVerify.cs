using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Detached filter-chain create-or-verify (Safe Deployment Spec §19 / Compiler Spec §26 / M4-04).
/// Pure Domain: ordered prefix recovery, no move/set/remove advice.
/// </summary>
public static class FilterChainCreateOrVerify
{
    private static readonly HashSet<string> BuiltinChainNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "input",
        "forward",
        "output",
    };

    /// <summary>
    /// Spec §17 staging order: company deny → site deny → node deny → root.
    /// </summary>
    public static IReadOnlyList<ChainArtifactDraft> OrderForStaging(IEnumerable<ChainArtifactDraft> chains)
    {
        ArgumentNullException.ThrowIfNull(chains);
        return chains
            .OrderBy(static c => RoleStagingRank(c.Role))
            .ThenBy(static c => RouterOsFilterArtifactIdentity.FormatFamily(c.Family), StringComparer.Ordinal)
            .ThenBy(static c => RouterOsFilterArtifactIdentity.FormatBuiltIn(c.BuiltInContext), StringComparer.Ordinal)
            .ThenBy(static c => c.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static int RoleStagingRank(FilterChainArtifactRole role)
        => role switch
        {
            FilterChainArtifactRole.CompanyDeny => 0,
            FilterChainArtifactRole.SiteDeny => 1,
            FilterChainArtifactRole.NodeDeny => 2,
            FilterChainArtifactRole.Root => 3,
            _ => throw new DomainInvariantException($"Unsupported chain role '{role}' for staging."),
        };

    /// <summary>
    /// Plans staging for one detached chain. Caller must re-read before every attempt (Spec §19 / §51).
    /// </summary>
    public static FilterChainStagingPlan Plan(
        ChainArtifactDraft desired,
        IReadOnlyList<ActualFilterChainRule> actualInOrder,
        IReadOnlySet<string>? activeRootChainNames = null)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(actualInOrder);

        if (string.IsNullOrWhiteSpace(desired.Name))
        {
            return FilterChainStagingPlan.Fail(
                DeploymentCodes.StagingRuleInvalid,
                "Desired chain name is required.");
        }

        string name = desired.Name.Trim();
        if (BuiltinChainNames.Contains(name) || !ActualFilterMarker.IsManagedChainName(name))
        {
            return FilterChainStagingPlan.Fail(
                DeploymentCodes.StagingRuleInvalid,
                $"Chain '{name}' is not a detached managed staging target.");
        }

        if (activeRootChainNames is not null
            && activeRootChainNames.Contains(name))
        {
            return FilterChainStagingPlan.Fail(
                DeploymentCodes.StagingResourceCollision,
                $"Active root chain '{name}' must not be used as a staging target.");
        }

        FilterRuleArtifact[] desiredRules = desired.Rules
            .OrderBy(static r => r.Ordinal)
            .ToArray();
        List<ActualFilterChainRule> scoped = actualInOrder
            .Where(r => string.Equals(r.Chain, name, StringComparison.Ordinal))
            .ToList();

        HashSet<string> seenComments = new(StringComparer.Ordinal);
        for (int i = 0; i < scoped.Count; i++)
        {
            ActualFilterChainRule row = scoped[i];
            if (row.Dynamic)
            {
                return FilterChainStagingPlan.Fail(
                    DeploymentCodes.StagingRuleInvalid,
                    $"Dynamic rule in chain '{name}' blocks staging.");
            }

            if (row.Disabled || row.Invalid)
            {
                return FilterChainStagingPlan.Fail(
                    DeploymentCodes.StagingRuleInvalid,
                    $"Disabled or invalid rule in chain '{name}' blocks staging.");
            }

            if (ActualFilterMarker.IsUnmanaged(row.Comment))
            {
                return FilterChainStagingPlan.Fail(
                    DeploymentCodes.StagingResourceCollision,
                    $"Unmanaged rule in generated chain '{name}' blocks staging.");
            }

            if (row.Comment is not null && !seenComments.Add(row.Comment))
            {
                return FilterChainStagingPlan.Fail(
                    DeploymentCodes.StagingResourceCollision,
                    $"Duplicate ownership comment '{row.Comment}' in chain '{name}'.");
            }
        }

        if (scoped.Count == 0)
        {
            return FilterChainStagingPlan.CreateAll(desiredRules);
        }

        if (scoped.Count > desiredRules.Length)
        {
            return FilterChainStagingPlan.Fail(
                DeploymentCodes.StagingResourceCollision,
                $"Extra rules in chain '{name}' (no remove/move).");
        }

        for (int i = 0; i < scoped.Count; i++)
        {
            if (!MatchesDesired(desiredRules[i], scoped[i]))
            {
                // Correct comment with different content, or wrong order → collision / prefix diverged.
                if (string.Equals(desiredRules[i].Comment, scoped[i].Comment, StringComparison.Ordinal))
                {
                    return FilterChainStagingPlan.Fail(
                        DeploymentCodes.StagingResourceCollision,
                        $"Ownership comment matches but content diverges at ordinal {i} in '{name}'.");
                }

                return FilterChainStagingPlan.Fail(
                    DeploymentCodes.StagingPrefixDiverged,
                    $"Chain '{name}' diverges from desired prefix at ordinal {i}.");
            }
        }

        if (scoped.Count == desiredRules.Length)
        {
            return FilterChainStagingPlan.Reuse();
        }

        return FilterChainStagingPlan.AppendSuffix(desiredRules.Skip(scoped.Count).ToArray());
    }

    public static bool TryVerifyChainHash(
        ChainArtifactDraft desired,
        IReadOnlyList<ActualFilterChainRule> actualInOrder,
        out Hash256 observedHash,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(actualInOrder);
        FilterRuleArtifact[] desiredRules = desired.Rules.OrderBy(static r => r.Ordinal).ToArray();
        List<ActualFilterChainRule> scoped = actualInOrder
            .Where(r => string.Equals(r.Chain, desired.Name.Trim(), StringComparison.Ordinal))
            .ToList();
        if (scoped.Count != desiredRules.Length)
        {
            observedHash = Hash256.Create(new byte[Hash256.Size]);
            error = DeploymentCodes.StagingArtifactHashMismatch;
            return false;
        }

        for (int i = 0; i < desiredRules.Length; i++)
        {
            if (!MatchesDesired(desiredRules[i], scoped[i]))
            {
                observedHash = Hash256.Create(new byte[Hash256.Size]);
                error = DeploymentCodes.StagingArtifactHashMismatch;
                return false;
            }
        }

        observedHash = RouterOsFilterArtifactIdentity.HashChainContent(
            desired.Family,
            desired.BuiltInContext,
            desired.Role,
            desired.Name,
            desiredRules);
        Hash256 expected = observedHash;
        error = null;
        return expected.Equals(observedHash);
    }

    public static bool MatchesDesired(FilterRuleArtifact desired, ActualFilterChainRule actual)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(actual);
        if (!string.Equals(desired.Comment, actual.Comment, StringComparison.Ordinal)
            || !string.Equals(desired.Action, actual.Action, StringComparison.OrdinalIgnoreCase)
            || desired.Log != actual.Log
            || !string.Equals(desired.LogPrefix, actual.LogPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach ((string key, string value) in desired.Matchers)
        {
            string? actualValue = actual.Get(key);
            if (!string.Equals(actualValue, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach ((string key, string value) in desired.ActionParameters)
        {
            if (!string.Equals(actual.Get(key), value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
