namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Persisted RoutingAssuranceState shell per Device (M7.1-02).
/// Configuration and operational snapshots are separate jsonb columns with distinct hashes.
/// Deferred slots (expectations/findings/traces) are typed empty jsonb arrays until later issues.
/// </summary>
public sealed class RoutingAssuranceStateEntity
{
    public Guid DeviceId { get; set; }

    public byte[] ConfigurationHash { get; set; } = [];

    public byte[] OperationalHash { get; set; } = [];

    public string ConfigurationJson { get; set; } = "{}";

    public string OperationalJson { get; set; } = "{}";

    public string RouteExpectationsJson { get; set; } = "[]";

    public string RouteFindingsJson { get; set; } = "[]";

    public string ResolutionTracesJson { get; set; } = "[]";

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long RowVersion { get; set; }
}
