using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL-backed application DbContext. Production database is PostgreSQL only.
/// </summary>
public sealed class MfcDbContext : DbContext
{
    public MfcDbContext(DbContextOptions<MfcDbContext> options)
        : base(options)
    {
    }

    public DbSet<ControllerInstanceEntity> ControllerInstances => Set<ControllerInstanceEntity>();

    public DbSet<SchemaMetadataEntity> SchemaMetadata => Set<SchemaMetadataEntity>();

    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    public DbSet<EncryptedSecretEntity> EncryptedSecrets => Set<EncryptedSecretEntity>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    public DbSet<SiteEntity> Sites => Set<SiteEntity>();

    public DbSet<NodeEntity> Nodes => Set<NodeEntity>();

    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();

    public DbSet<DeviceConnectionProfileEntity> DeviceConnectionProfiles => Set<DeviceConnectionProfileEntity>();

    public DbSet<CaptureOperationEntity> CaptureOperations => Set<CaptureOperationEntity>();

    public DbSet<SnapshotPayloadEntity> SnapshotPayloads => Set<SnapshotPayloadEntity>();

    public DbSet<SnapshotCaptureEntity> SnapshotCaptures => Set<SnapshotCaptureEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MfcDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Blocks update/delete of audit events and immutable snapshot rows at the DbContext boundary.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforcePersistenceInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc cref="SaveChanges(bool)"/>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforcePersistenceInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforcePersistenceInvariants()
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<AuditEventEntity> entry
                 in ChangeTracker.Entries<AuditEventEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "audit_events is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SnapshotPayloadEntity> entry
                 in ChangeTracker.Entries<SnapshotPayloadEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "snapshot_payloads is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SnapshotCaptureEntity> entry
                 in ChangeTracker.Entries<SnapshotCaptureEntity>())
        {
            if (entry.State is EntityState.Deleted
                && entry.Entity.Status == SnapshotCaptureEntity.CompletedStatus)
            {
                throw new InvalidOperationException(
                    "Completed snapshot_captures cannot be deleted through the application DbContext.");
            }

            if (entry.State is EntityState.Modified
                && entry.Property(e => e.Status).OriginalValue == SnapshotCaptureEntity.CompletedStatus)
            {
                throw new InvalidOperationException(
                    "Completed snapshot_captures are immutable and cannot be updated through the application DbContext.");
            }
        }
    }
}
