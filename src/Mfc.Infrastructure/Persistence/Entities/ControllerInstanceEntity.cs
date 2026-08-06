namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Registered Controller process identity for operational awareness.
/// </summary>
public sealed class ControllerInstanceEntity
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }

    public required string HostName { get; set; }

    public int? ProcessId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public required string ApplicationVersion { get; set; }

    public required string Status { get; set; }
}
