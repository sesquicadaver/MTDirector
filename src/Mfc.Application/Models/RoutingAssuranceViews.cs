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
