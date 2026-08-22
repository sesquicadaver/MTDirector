namespace Mfc.Application.Models;

/// <summary>Application view of persisted routing assurance state (hashes as lowercase hex).</summary>
public sealed class RoutingAssuranceStateView
{
    public required Guid DeviceId { get; init; }

    public required string ConfigurationHashHex { get; init; }

    public required string OperationalHashHex { get; init; }

    public required int RouteExpectationCount { get; init; }

    public required int RouteFindingCount { get; init; }

    public required int ResolutionTraceCount { get; init; }

    public required int ConfigurationTableCount { get; init; }

    public required int ConfigurationRuleCount { get; init; }

    public required int ConfigurationVrfCount { get; init; }

    public required int ConfigurationStaticRouteCount { get; init; }

    public required int ConfigurationFilterRuleCount { get; init; }

    public required int OperationalRouteCount { get; init; }

    public required int OperationalDefaultRouteCount { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public required ulong RowVersion { get; init; }
}

/// <summary>Read model for Desktop routing assurance viewers (M7.1-10); summaries only.</summary>
public sealed class RoutingAssuranceDetailView
{
    public required Guid DeviceId { get; init; }

    public required string ConfigurationHashHex { get; init; }

    public required string OperationalHashHex { get; init; }

    public required int RouteExpectationCount { get; init; }

    public required int RouteFindingCount { get; init; }

    public required int ResolutionTraceCount { get; init; }

    public required int ConfigurationTableCount { get; init; }

    public required int ConfigurationRuleCount { get; init; }

    public required int ConfigurationVrfCount { get; init; }

    public required int ConfigurationStaticRouteCount { get; init; }

    public required int ConfigurationFilterRuleCount { get; init; }

    public required int OperationalRouteCount { get; init; }

    public required int OperationalDefaultRouteCount { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public required ulong RowVersion { get; init; }

    public IReadOnlyList<RouteExpectationView> Expectations { get; init; } = [];

    public IReadOnlyList<RouteFindingView> Findings { get; init; } = [];

    public IReadOnlyList<RouteResolutionTraceSummaryView> TraceSummaries { get; init; } = [];
}

/// <summary>Declarative route expectation row for read surfaces.</summary>
public sealed class RouteExpectationView
{
    public Guid? NodeId { get; init; }

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

    public bool ExpectAsymmetricReversePath { get; init; }

    public bool Critical { get; init; }
}

/// <summary>Routing assurance finding row for read surfaces.</summary>
public sealed class RouteFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>
/// Bounded route resolution trace summary (M7.1 Spec §10).
/// Excludes full route_candidates, recursive_resolution, and operational route dumps.
/// </summary>
public sealed class RouteResolutionTraceSummaryView
{
    public const int MaxNextHopGateways = 8;

    public const int MaxEgressInterfaces = 8;

    public required string Family { get; init; }

    public string? DestinationAddress { get; init; }

    public string? SourceAddress { get; init; }

    public string? SelectedVrf { get; init; }

    public string? SelectedTable { get; init; }

    public string? MatchedPrefix { get; init; }

    public IReadOnlyList<string> NextHopGateways { get; init; } = [];

    public IReadOnlyList<string> EgressInterfaces { get; init; } = [];

    public string? ExecutionPath { get; init; }

    public string? Decision { get; init; }

    public IReadOnlyList<string> DriftCodes { get; init; } = [];

    public IReadOnlyList<string> LatencyCodes { get; init; } = [];

    public string? ReversePathSymmetryResult { get; init; }
}
