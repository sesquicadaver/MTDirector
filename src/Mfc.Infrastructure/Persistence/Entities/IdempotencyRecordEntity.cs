namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Durable idempotency key for mutating Controller operations.
/// </summary>
public sealed class IdempotencyRecordEntity
{
    public required string Key { get; set; }

    public required string Actor { get; set; }

    public required string Operation { get; set; }

    public required byte[] RequestHash { get; set; }

    public string? ResponseRef { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
