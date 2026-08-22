namespace Mfc.Domain.Routing;

/// <summary>
/// Declarative route expectation (M7.1 Spec §11).
/// Evaluated by <see cref="RouteExpectationEvaluator"/> against <see cref="RouteResolutionTrace"/> probes.
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

    /// <summary>When set with <see cref="RequireReversePath"/>, asymmetry does not produce a finding.</summary>
    public bool ExpectAsymmetricReversePath { get; init; }

    public bool Critical { get; init; }
}
