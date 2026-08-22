namespace Mfc.Domain.Routing;

/// <summary>
/// Declarative route expectation shell (M7.1 Spec §11).
/// Evaluation and population are deferred to <c>M7.1-06</c> (#115).
/// Persistence stores an empty typed collection until that issue.
/// </summary>
public sealed class RouteExpectation
{
    public required Guid? NodeId { get; init; }

    public required string Family { get; init; }

    public string? SourceZone { get; init; }

    public string? SourceAddress { get; init; }

    public required string DestinationPrefix { get; init; }

    public string? ExpectedVrf { get; init; }

    public string? ExpectedTable { get; init; }

    public IReadOnlyList<string> AllowedNextHops { get; init; } = [];

    public IReadOnlyList<string> AllowedEgressZones { get; init; } = [];

    public IReadOnlyList<string> AllowedEgressInterfaces { get; init; } = [];

    public IReadOnlyList<string> RequiredRouteTypes { get; init; } = [];

    public IReadOnlyList<string> ForbiddenRouteTypes { get; init; } = [];

    public bool RequireCpuFirewallPath { get; init; }

    public bool RequireReversePath { get; init; }

    public bool Critical { get; init; }
}

/// <summary>
/// Routing assurance finding shell (Spec §5–§14 findings).
/// Population is deferred to later M7.1 analysis issues; slot is typed and persisted as [].
/// </summary>
public sealed class RouteFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

