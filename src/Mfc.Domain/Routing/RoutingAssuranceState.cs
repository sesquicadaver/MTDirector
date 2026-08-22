using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Routing;

/// <summary>
/// Persisted routing-assurance state shell per Device (M7.1 Spec §2 / M7.1-02).
/// Separates <see cref="Configuration"/> from <see cref="OperationalState"/> via distinct hashes.
/// <see cref="RouteExpectations"/> and <see cref="RouteFindings"/> are populated by M7.1-06 evaluation
/// (reverse-path checks delegate to M7.1-07 <see cref="ReversePathSymmetryAnalyzer"/>);
/// <see cref="ResolutionTraces"/> by M7.1-03 trace analysis with optional M7.1-07 symmetry attachment.
/// </summary>
public sealed class RoutingAssuranceState : IEquatable<RoutingAssuranceState>
{
    public DeviceId DeviceId { get; }

    public RoutingConfigurationSnapshot Configuration { get; }

    public RoutingOperationalSnapshot OperationalState { get; }

    public Hash256 ConfigurationHash { get; }

    public Hash256 OperationalHash { get; }

    /// <summary>Declarative route expectations (M7.1 Spec §11).</summary>
    public IReadOnlyList<RouteExpectation> RouteExpectations { get; }

    /// <summary>Findings from expectation evaluation and later routing analysis.</summary>
    public IReadOnlyList<RouteFinding> RouteFindings { get; }

    /// <summary>Route resolution traces (M7.1-03); empty when no probes were analyzed.</summary>
    public IReadOnlyList<RouteResolutionTrace> ResolutionTraces { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public ulong RowVersion { get; }

    private RoutingAssuranceState(
        DeviceId deviceId,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operationalState,
        Hash256 configurationHash,
        Hash256 operationalHash,
        IReadOnlyList<RouteExpectation> routeExpectations,
        IReadOnlyList<RouteFinding> routeFindings,
        IReadOnlyList<RouteResolutionTrace> resolutionTraces,
        DateTimeOffset updatedAtUtc,
        ulong rowVersion)
    {
        DeviceId = deviceId;
        Configuration = configuration;
        OperationalState = operationalState;
        ConfigurationHash = configurationHash;
        OperationalHash = operationalHash;
        RouteExpectations = routeExpectations;
        RouteFindings = routeFindings;
        ResolutionTraces = resolutionTraces;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        RowVersion = rowVersion;
    }

    /// <summary>
    /// Creates a new state row. Hashes are derived from snapshot materials.
    /// Deferred slots default to empty collections (not null).
    /// </summary>
    public static RoutingAssuranceState Create(
        DeviceId deviceId,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operationalState,
        DateTimeOffset updatedAtUtc,
        IReadOnlyList<RouteExpectation>? routeExpectations = null,
        IReadOnlyList<RouteFinding>? routeFindings = null,
        IReadOnlyList<RouteResolutionTrace>? resolutionTraces = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operationalState);
        return new RoutingAssuranceState(
            deviceId,
            configuration,
            operationalState,
            RoutingAssuranceHashContract.HashConfiguration(configuration.HashMaterial),
            RoutingAssuranceHashContract.HashOperational(operationalState.HashMaterial),
            routeExpectations ?? [],
            routeFindings ?? [],
            resolutionTraces ?? [],
            updatedAtUtc,
            rowVersion: 1);
    }

    /// <summary>Rebuilds state from persistence (hashes must match stored digests).</summary>
    public static RoutingAssuranceState Reconstitute(
        DeviceId deviceId,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operationalState,
        Hash256 configurationHash,
        Hash256 operationalHash,
        IReadOnlyList<RouteExpectation> routeExpectations,
        IReadOnlyList<RouteFinding> routeFindings,
        IReadOnlyList<RouteResolutionTrace> resolutionTraces,
        DateTimeOffset updatedAtUtc,
        ulong rowVersion)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operationalState);
        ArgumentNullException.ThrowIfNull(configurationHash);
        ArgumentNullException.ThrowIfNull(operationalHash);
        ArgumentNullException.ThrowIfNull(routeExpectations);
        ArgumentNullException.ThrowIfNull(routeFindings);
        ArgumentNullException.ThrowIfNull(resolutionTraces);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("RoutingAssuranceState row_version must be greater than zero.");
        }

        return new RoutingAssuranceState(
            deviceId,
            configuration,
            operationalState,
            configurationHash,
            operationalHash,
            routeExpectations,
            routeFindings,
            resolutionTraces,
            updatedAtUtc,
            rowVersion);
    }

    /// <summary>Returns a copy with updated snapshots/slots and bumped row version.</summary>
    public RoutingAssuranceState With(
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operationalState,
        DateTimeOffset updatedAtUtc,
        IReadOnlyList<RouteExpectation>? routeExpectations = null,
        IReadOnlyList<RouteFinding>? routeFindings = null,
        IReadOnlyList<RouteResolutionTrace>? resolutionTraces = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operationalState);
        return new RoutingAssuranceState(
            DeviceId,
            configuration,
            operationalState,
            RoutingAssuranceHashContract.HashConfiguration(configuration.HashMaterial),
            RoutingAssuranceHashContract.HashOperational(operationalState.HashMaterial),
            routeExpectations ?? RouteExpectations,
            routeFindings ?? RouteFindings,
            resolutionTraces ?? ResolutionTraces,
            updatedAtUtc,
            RowVersion + 1);
    }

    public bool Equals(RoutingAssuranceState? other)
    {
        if (other is null)
        {
            return false;
        }

        return DeviceId.Equals(other.DeviceId)
               && ConfigurationHash.Equals(other.ConfigurationHash)
               && OperationalHash.Equals(other.OperationalHash)
               && RowVersion == other.RowVersion;
    }

    public override bool Equals(object? obj) => obj is RoutingAssuranceState other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(DeviceId, ConfigurationHash, OperationalHash, RowVersion);
}
