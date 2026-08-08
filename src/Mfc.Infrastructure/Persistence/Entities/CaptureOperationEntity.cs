namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Operator-requested capture operation spanning one or more devices (Vertical Slice §8.6).</summary>
public sealed class CaptureOperationEntity
{
    public Guid Id { get; set; }

    public short TargetType { get; set; }

    public Guid TargetId { get; set; }

    public Guid RequestedBy { get; set; }

    public Guid IdempotencyKey { get; set; }

    public short Status { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ErrorCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
