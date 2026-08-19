namespace Mfc.Desktop.Services;

public sealed class OnboardingFindingListItem
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string SummaryLine => $"{Severity} {Code}: {Message}";
}

public sealed class OnboardingPlacementListItem
{
    public required string Marker { get; init; }

    public required string Mode { get; init; }

    public required string BeforeLabel { get; init; }

    public required string AfterLabel { get; init; }

    public string SummaryLine => $"{Marker} · {Mode} · before={BeforeLabel} after={AfterLabel}";
}
