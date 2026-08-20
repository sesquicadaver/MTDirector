using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Outcome of staging one detached filter chain (Safe Deployment Spec §19 / M4-04).</summary>
public sealed class FilterChainStagingResult
{
    public required bool Succeeded { get; init; }

    public required string ChainName { get; init; }

    public required FilterChainStagingAction Action { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public Hash256? ObservedChainHash { get; init; }

    public required int AddedCount { get; init; }

    public required int ReadBeforeWriteCount { get; init; }
}

/// <summary>Aggregate detached-chain staging for one artifact slice (deny before root).</summary>
public sealed class DetachedChainsStagingResult
{
    public required bool Succeeded { get; init; }

    /// <summary>True only when every chain succeeded and hashes verified — never for partial (AC#10).</summary>
    public required bool ArtifactStaged { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<FilterChainStagingResult> Chains { get; init; }

    public required int TotalAddedCount { get; init; }
}

/// <summary>
/// Stages detached managed filter chains via create-or-verify (M4-04).
/// Deny chains are ordered before root; active roots are rejected; no move/set/remove.
/// </summary>
public static class StageDetachedChainsUseCase
{
    public static async Task<DetachedChainsStagingResult> ExecuteAsync(
        IReadOnlyList<ChainArtifactDraft> chains,
        IRouterOsDeploymentSession session,
        IReadOnlySet<string>? activeRootChainNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chains);
        ArgumentNullException.ThrowIfNull(session);

        IReadOnlyList<ChainArtifactDraft> ordered = FilterChainCreateOrVerify.OrderForStaging(chains);
        List<FilterChainStagingResult> results = [];
        int totalAdded = 0;

        foreach (ChainArtifactDraft chain in ordered)
        {
            FilterChainStagingResult one = await StageOneAsync(
                chain,
                session,
                activeRootChainNames,
                cancellationToken).ConfigureAwait(false);
            results.Add(one);
            totalAdded += one.AddedCount;
            if (!one.Succeeded)
            {
                return new DetachedChainsStagingResult
                {
                    Succeeded = false,
                    ArtifactStaged = false,
                    Code = one.Code,
                    Message = one.Message,
                    Chains = results,
                    TotalAddedCount = totalAdded,
                };
            }
        }

        return new DetachedChainsStagingResult
        {
            Succeeded = true,
            ArtifactStaged = true,
            Chains = results,
            TotalAddedCount = totalAdded,
        };
    }

    public static async Task<FilterChainStagingResult> StageOneAsync(
        ChainArtifactDraft desired,
        IRouterOsDeploymentSession session,
        IReadOnlySet<string>? activeRootChainNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(session);

        int readCount = 0;
        ActualManagedState state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        readCount++;
        List<ActualFilterChainRule> actual = Map(desired.Family, state);

        FilterChainStagingPlan plan = FilterChainCreateOrVerify.Plan(desired, actual, activeRootChainNames);
        if (!plan.Succeeded)
        {
            return Fail(desired.Name, plan, readCount);
        }

        if (plan.Action == FilterChainStagingAction.Reuse)
        {
            if (!FilterChainCreateOrVerify.TryVerifyChainHash(desired, actual, out Hash256 hash, out string? verifyError))
            {
                return new FilterChainStagingResult
                {
                    Succeeded = false,
                    ChainName = desired.Name,
                    Action = FilterChainStagingAction.Collision,
                    Code = DeploymentCodes.StagingArtifactHashMismatch,
                    Message = verifyError,
                    ObservedChainHash = hash,
                    AddedCount = 0,
                    ReadBeforeWriteCount = readCount,
                };
            }

            return new FilterChainStagingResult
            {
                Succeeded = true,
                ChainName = desired.Name,
                Action = FilterChainStagingAction.Reuse,
                ObservedChainHash = hash,
                AddedCount = 0,
                ReadBeforeWriteCount = readCount,
            };
        }

        int added = 0;
        foreach (FilterRuleArtifact rule in plan.MissingRules)
        {
            if (added > 0)
            {
                state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
                readCount++;
                actual = Map(desired.Family, state);
                FilterChainStagingPlan refreshed = FilterChainCreateOrVerify.Plan(desired, actual, activeRootChainNames);
                if (!refreshed.Succeeded)
                {
                    return Fail(desired.Name, refreshed, readCount, added);
                }

                if (refreshed.Action == FilterChainStagingAction.Reuse)
                {
                    break;
                }

                if (!refreshed.MissingRules.Any(r => string.Equals(r.Comment, rule.Comment, StringComparison.Ordinal)))
                {
                    continue;
                }
            }

            FilterRuleWrite write = ToWrite(desired.Family, desired.Name, rule);
            DeploymentWriteExecutionResult exec = await session.AddFilterRuleAsync(write, cancellationToken)
                .ConfigureAwait(false);
            if (!exec.Succeeded)
            {
                return new FilterChainStagingResult
                {
                    Succeeded = false,
                    ChainName = desired.Name,
                    Action = FilterChainStagingAction.Collision,
                    Code = DeploymentCodes.StagingResourceCollision,
                    Message = exec.Error ?? "Filter rule add failed.",
                    AddedCount = added,
                    ReadBeforeWriteCount = readCount,
                };
            }

            added++;
        }

        state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        readCount++;
        actual = Map(desired.Family, state);
        if (!FilterChainCreateOrVerify.TryVerifyChainHash(desired, actual, out Hash256 observed, out string? error))
        {
            return new FilterChainStagingResult
            {
                Succeeded = false,
                ChainName = desired.Name,
                Action = FilterChainStagingAction.Collision,
                Code = DeploymentCodes.StagingArtifactHashMismatch,
                Message = error,
                ObservedChainHash = observed,
                AddedCount = added,
                ReadBeforeWriteCount = readCount,
            };
        }

        return new FilterChainStagingResult
        {
            Succeeded = true,
            ChainName = desired.Name,
            Action = plan.Action,
            ObservedChainHash = observed,
            AddedCount = added,
            ReadBeforeWriteCount = readCount,
        };
    }

    private static FilterRuleWrite ToWrite(IpAddressFamily family, string chain, FilterRuleArtifact rule)
    {
        string? jump = rule.Matchers.TryGetValue("jump-target", out string? jt) ? jt : null;
        Dictionary<string, string> matchers = new(StringComparer.Ordinal);
        foreach ((string key, string value) in rule.Matchers)
        {
            if (key is "jump-target")
            {
                continue;
            }

            matchers[key] = value;
        }

        foreach ((string key, string value) in rule.ActionParameters)
        {
            matchers[key] = value;
        }

        if (rule.Log)
        {
            matchers["log"] = "yes";
        }

        if (rule.LogPrefix is not null)
        {
            matchers["log-prefix"] = rule.LogPrefix;
        }

        return new FilterRuleWrite(
            family,
            chain,
            rule.Action,
            jumpTarget: jump,
            comment: rule.Comment,
            disabled: false,
            additionalMatchers: matchers);
    }

    private static FilterChainStagingResult Fail(
        string chainName,
        FilterChainStagingPlan plan,
        int readCount,
        int added = 0)
        => new()
        {
            Succeeded = false,
            ChainName = chainName,
            Action = plan.Action,
            Code = plan.Code,
            Message = plan.Message,
            AddedCount = added,
            ReadBeforeWriteCount = readCount,
        };

    private static List<ActualFilterChainRule> Map(IpAddressFamily family, ActualManagedState state)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = family == IpAddressFamily.IPv4
            ? state.Ipv4FilterRules
            : state.Ipv6FilterRules;
        List<ActualFilterChainRule> mapped = new(rows.Count);
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            string? chain = row.GetValueOrDefault("chain");
            string? action = row.GetValueOrDefault("action");
            if (string.IsNullOrWhiteSpace(chain) || string.IsNullOrWhiteSpace(action))
            {
                continue;
            }

            mapped.Add(new ActualFilterChainRule(
                chain,
                action,
                comment: row.GetValueOrDefault("comment"),
                disabled: Yes(row.GetValueOrDefault("disabled")),
                invalid: Yes(row.GetValueOrDefault("invalid")),
                dynamic: Yes(row.GetValueOrDefault("dynamic")),
                log: Yes(row.GetValueOrDefault("log")),
                logPrefix: row.GetValueOrDefault("log-prefix"),
                properties: new Dictionary<string, string>(row, StringComparer.Ordinal)));
        }

        return mapped;
    }

    private static bool Yes(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
