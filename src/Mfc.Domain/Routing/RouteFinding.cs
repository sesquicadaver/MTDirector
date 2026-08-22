namespace Mfc.Domain.Routing;

/// <summary>
/// Routing assurance finding produced by expectation evaluation or later analysis (M7.1 Spec §5–§14).
/// </summary>
public sealed class RouteFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}
