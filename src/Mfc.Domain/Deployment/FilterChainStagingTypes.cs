using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>One live filter rule row in a detached chain (Safe Deployment Spec §19).</summary>
public sealed class ActualFilterChainRule
{
    public ActualFilterChainRule(
        string chain,
        string action,
        string? comment = null,
        bool disabled = false,
        bool invalid = false,
        bool dynamic = false,
        bool log = false,
        string? logPrefix = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chain);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Chain = chain.Trim();
        Action = action.Trim();
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        Disabled = disabled;
        Invalid = invalid;
        Dynamic = dynamic;
        Log = log;
        LogPrefix = string.IsNullOrWhiteSpace(logPrefix) ? null : logPrefix.Trim();
        Properties = properties ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public string Chain { get; }

    public string Action { get; }

    public string? Comment { get; }

    public bool Disabled { get; }

    public bool Invalid { get; }

    public bool Dynamic { get; }

    public bool Log { get; }

    public string? LogPrefix { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }

    public string? Get(string key)
        => Properties.TryGetValue(key, out string? value) ? value : null;
}

/// <summary>Create-or-verify decision for one detached filter chain (Spec §19).</summary>
public enum FilterChainStagingAction : byte
{
    Reuse = 0,
    CreateAll = 1,
    AppendSuffix = 2,
    Collision = 3,
}

/// <summary>Pure planner result for one chain — no RouterOS I/O.</summary>
public sealed class FilterChainStagingPlan
{
    public required bool Succeeded { get; init; }

    public required FilterChainStagingAction Action { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    /// <summary>Desired rules to append in ordinal order (suffix or full create).</summary>
    public required IReadOnlyList<FilterRuleArtifact> MissingRules { get; init; }

    public static FilterChainStagingPlan Reuse()
        => new()
        {
            Succeeded = true,
            Action = FilterChainStagingAction.Reuse,
            MissingRules = [],
        };

    public static FilterChainStagingPlan CreateAll(IReadOnlyList<FilterRuleArtifact> rules)
        => new()
        {
            Succeeded = true,
            Action = FilterChainStagingAction.CreateAll,
            MissingRules = rules,
        };

    public static FilterChainStagingPlan AppendSuffix(IReadOnlyList<FilterRuleArtifact> suffix)
        => new()
        {
            Succeeded = true,
            Action = FilterChainStagingAction.AppendSuffix,
            MissingRules = suffix,
        };

    public static FilterChainStagingPlan Fail(string code, string message)
        => new()
        {
            Succeeded = false,
            Action = FilterChainStagingAction.Collision,
            Code = code,
            Message = message,
            MissingRules = [],
        };
}
