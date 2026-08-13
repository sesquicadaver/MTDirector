using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Presentation row for a policy rule (read-only Desktop panel).</summary>
public sealed class PolicyRuleListItem
{
    public required Guid Id { get; init; }

    public required string FamilyText { get; init; }

    public required string ChainText { get; init; }

    public required string StageText { get; init; }

    public required uint Ordinal { get; init; }

    public required bool Enabled { get; init; }

    public required string EffectText { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> WarningLines { get; init; }

    public string SummaryLine
    {
        get
        {
            string enabled = Enabled ? "on" : "off";
            string warnings = WarningLines.Count == 0
                ? string.Empty
                : " | " + string.Join("; ", WarningLines);
            return $"#{Ordinal} {FamilyText}/{ChainText}/{StageText} {EffectText} [{enabled}] {Description}{warnings}";
        }
    }
}

/// <summary>Desktop policy panel orchestration over Contracts-only client.</summary>
public interface IPolicyPanelService
{
    Task<IReadOnlyList<PolicyRuleListItem>> ListRulesAsync(
        Guid revisionId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);
}

/// <summary>Default thin policy panel service (list rules for a revision).</summary>
public sealed class PolicyPanelService : IPolicyPanelService
{
    private readonly IPolicyServiceClient _client;

    public PolicyPanelService(IPolicyServiceClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IReadOnlyList<PolicyRuleListItem>> ListRulesAsync(
        Guid revisionId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        ListRulesResponse response = await _client
            .ListRulesAsync(revisionId, activeOnly, cancellationToken)
            .ConfigureAwait(false);
        return response.Rules.Select(ToItem).ToArray();
    }

    private static PolicyRuleListItem ToItem(PolicyRule rule) => new()
    {
        Id = DesktopProtoUuid.ToGuid(rule.Id),
        FamilyText = rule.Family.ToString(),
        ChainText = rule.Chain.ToString(),
        StageText = rule.Stage.ToString(),
        Ordinal = rule.Ordinal,
        Enabled = rule.Enabled,
        EffectText = rule.Effect?.Kind.ToString() ?? "Unspecified",
        Description = rule.Description,
        WarningLines = rule.Warnings
            .Select(w => string.IsNullOrWhiteSpace(w.Subject) ? $"{w.Code}: {w.Message}" : $"{w.Code}({w.Subject}): {w.Message}")
            .ToArray(),
    };
}
