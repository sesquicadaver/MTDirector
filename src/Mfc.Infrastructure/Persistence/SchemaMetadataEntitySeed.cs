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

    public const string SnapshotPersistSchemaKey = "snapshot.persist.schema";
    public const string SnapshotPersistSchemaValue = "m1-23";

    public const string PolicyLifecycleSchemaKey = "policy.lifecycle.schema";
    public const string PolicyLifecycleSchemaValue = "m2-01";

    public const string ZoneBindingsSchemaKey = "policy.zone_bindings.schema";
    public const string ZoneBindingsSchemaValue = "m2-05";

    public const string PolicyApprovalSchemaKey = "policy.approval.schema";
    public const string PolicyApprovalSchemaValue = "m2-17";

    public static void EnsureBootstrapMetadata(MfcDbContext db)
    {
        EnsureKey(db, BootstrapSchemaKey, BootstrapSchemaValue);
        EnsureKey(db, InventorySnapshotSchemaKey, InventorySnapshotSchemaValue);
        EnsureKey(db, SnapshotPersistSchemaKey, SnapshotPersistSchemaValue);
        EnsureKey(db, PolicyLifecycleSchemaKey, PolicyLifecycleSchemaValue);
        EnsureKey(db, ZoneBindingsSchemaKey, ZoneBindingsSchemaValue);
        EnsureKey(db, PolicyApprovalSchemaKey, PolicyApprovalSchemaValue);
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
