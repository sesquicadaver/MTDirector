namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Application-level schema metadata (distinct from EF migrations history).
/// </summary>
public sealed class SchemaMetadataEntity
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
