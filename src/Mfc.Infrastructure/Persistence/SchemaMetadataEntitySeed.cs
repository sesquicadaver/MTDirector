using Mfc.Infrastructure.Persistence.Entities;

namespace Mfc.Infrastructure.Persistence;

/// <summary>
/// Seeds application schema metadata after migrations when missing.
/// </summary>
public static class SchemaMetadataEntitySeed
{
    public const string BootstrapSchemaKey = "bootstrap.schema";
    public const string BootstrapSchemaValue = "m0-07";

    public const string InventorySnapshotSchemaKey = "inventory.snapshot.schema";
    public const string InventorySnapshotSchemaValue = "m1-03";

    public static void EnsureBootstrapMetadata(MfcDbContext db)
    {
        EnsureKey(db, BootstrapSchemaKey, BootstrapSchemaValue);
        EnsureKey(db, InventorySnapshotSchemaKey, InventorySnapshotSchemaValue);
    }

    private static void EnsureKey(MfcDbContext db, string key, string value)
    {
        SchemaMetadataEntity? existing = db.SchemaMetadata.Find(key);
        if (existing is not null)
        {
            return;
        }

        db.SchemaMetadata.Add(new SchemaMetadataEntity
        {
            Key = key,
            Value = value,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
    }
}
