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

    public DbSet<SnapshotCaptureSectionEntity> SnapshotCaptureSections => Set<SnapshotCaptureSectionEntity>();

    public DbSet<PolicyEntity> Policies => Set<PolicyEntity>();

    public DbSet<PolicyRevisionEntity> PolicyRevisions => Set<PolicyRevisionEntity>();

    public DbSet<ZoneDefinitionEntity> ZoneDefinitions => Set<ZoneDefinitionEntity>();

    public DbSet<NodeZoneBindingEntity> NodeZoneBindings => Set<NodeZoneBindingEntity>();

    public DbSet<PolicyAnalysisRunEntity> PolicyAnalysisRuns => Set<PolicyAnalysisRunEntity>();

    public DbSet<PolicyWarningAcknowledgmentEntity> WarningAcknowledgments => Set<PolicyWarningAcknowledgmentEntity>();

    public DbSet<PolicyApprovalEntity> PolicyApprovals => Set<PolicyApprovalEntity>();

    public DbSet<PolicyBindingEntity> PolicyBindings => Set<PolicyBindingEntity>();

    public DbSet<FilterArtifactEntity> FilterArtifacts => Set<FilterArtifactEntity>();

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

        // Sections are written only with completed captures; treat the table as append-only.
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SnapshotCaptureSectionEntity> entry
                 in ChangeTracker.Entries<SnapshotCaptureSectionEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "snapshot_capture_sections is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PolicyRevisionEntity> entry
                 in ChangeTracker.Entries<PolicyRevisionEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "policy_revisions cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            short originalState = entry.Property(e => e.State).OriginalValue;
            bool payloadWasFrozen = originalState is PolicyRevisionEntity.ApprovedState
                or PolicyRevisionEntity.RejectedState
                or PolicyRevisionEntity.SupersededState
                or PolicyRevisionEntity.RevokedState;

            if (!payloadWasFrozen)
            {
                continue;
            }

            if (PayloadFieldsModified(entry))
            {
                throw new InvalidOperationException(
                    "Approved/terminal policy_revision payload is immutable and cannot be updated through the application DbContext.");
            }

            // APPROVED may transition to SUPERSEDED/REVOKED; other terminal states are frozen.
            if (originalState != PolicyRevisionEntity.ApprovedState)
            {
                throw new InvalidOperationException(
                    "Terminal policy_revision state cannot be updated through the application DbContext.");
            }

            short newState = entry.Entity.State;
            if (newState is not (PolicyRevisionEntity.SupersededState or PolicyRevisionEntity.RevokedState))
            {
                throw new InvalidOperationException(
                    "APPROVED policy_revision may only transition to SUPERSEDED or REVOKED through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PolicyAnalysisRunEntity> entry
                 in ChangeTracker.Entries<PolicyAnalysisRunEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "policy_analysis_runs is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PolicyWarningAcknowledgmentEntity> entry
                 in ChangeTracker.Entries<PolicyWarningAcknowledgmentEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "warning_acknowledgments is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PolicyApprovalEntity> entry
                 in ChangeTracker.Entries<PolicyApprovalEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "policy_approvals is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PolicyBindingEntity> entry
                 in ChangeTracker.Entries<PolicyBindingEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "policy_bindings cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (entry.Property(e => e.Scope).IsModified
                || entry.Property(e => e.ScopeId).IsModified
                || entry.Property(e => e.PolicyId).IsModified
                || entry.Property(e => e.DesiredRevisionId).IsModified
                || entry.Property(e => e.AnalysisRunId).IsModified
                || entry.Property(e => e.BundleHash).IsModified
                || entry.Property(e => e.ValidFromUtc).IsModified
                || entry.Property(e => e.ValidUntilUtc).IsModified
                || entry.Property(e => e.CreatedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Desired-binding identity and validity window cannot be updated through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<FilterArtifactEntity> entry
                 in ChangeTracker.Entries<FilterArtifactEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "filter_artifacts is append-only: update and delete are not allowed through the application DbContext.");
            }
        }
    }

    private static bool PayloadFieldsModified(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PolicyRevisionEntity> entry)
        => entry.Property(e => e.ContentHash).IsModified
           || entry.Property(e => e.ParentContextHash).IsModified
           || entry.Property(e => e.CompressedPayload).IsModified
           || entry.Property(e => e.Compression).IsModified
           || entry.Property(e => e.UncompressedSize).IsModified
           || entry.Property(e => e.SchemaVersion).IsModified
           || entry.Property(e => e.RevisionNumber).IsModified
           || entry.Property(e => e.PolicyId).IsModified
           || entry.Property(e => e.CreatedBy).IsModified
           || entry.Property(e => e.CreatedAtUtc).IsModified;
}
