using Mfc.Infrastructure.Persistence.Entities;

namespace Mfc.Infrastructure.Persistence;

/// <summary>
/// Seeds application schema metadata after migrations when missing.
/// </summary>
public static class SchemaMetadataEntitySeed
{
    public const string BootstrapSchemaKey = "bootstrap.schema";
    public const string BootstrapSchemaValue = "m0-07";

    public static void EnsureBootstrapMetadata(MfcDbContext db)
    {
        SchemaMetadataEntity? existing = db.SchemaMetadata.Find(BootstrapSchemaKey);
        if (existing is not null)
        {
            return;
        }

        db.SchemaMetadata.Add(new SchemaMetadataEntity
        {
            Key = BootstrapSchemaKey,
            Value = BootstrapSchemaValue,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
    }
}
