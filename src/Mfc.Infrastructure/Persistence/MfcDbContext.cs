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

    public DbSet<DeviceHashStateEntity> DeviceHashStates => Set<DeviceHashStateEntity>();

    public DbSet<RoutingAssuranceStateEntity> RoutingAssuranceStates => Set<RoutingAssuranceStateEntity>();

    public DbSet<EndpointPresenceIntervalEntity> EndpointPresenceIntervals => Set<EndpointPresenceIntervalEntity>();

    public DbSet<EndpointRoutingContextEntity> EndpointRoutingContexts => Set<EndpointRoutingContextEntity>();

    public DbSet<ResponseAssessmentEntity> ResponseAssessments => Set<ResponseAssessmentEntity>();

    public DbSet<DriftEventEntity> DriftEvents => Set<DriftEventEntity>();

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

    public DbSet<OnboardingPlanEntity> OnboardingPlans => Set<OnboardingPlanEntity>();

    public DbSet<OnboardingDevicePlanEntity> OnboardingDevicePlans => Set<OnboardingDevicePlanEntity>();

    public DbSet<OnboardingAnchorPlacementEntity> OnboardingAnchorPlacements => Set<OnboardingAnchorPlacementEntity>();

    public DbSet<OnboardingOperationEntity> OnboardingOperations => Set<OnboardingOperationEntity>();

    public DbSet<OnboardingStepEntity> OnboardingSteps => Set<OnboardingStepEntity>();

    public DbSet<DeploymentPlanEntity> DeploymentPlans => Set<DeploymentPlanEntity>();

    public DbSet<DeploymentDevicePlanEntity> DeploymentDevicePlans => Set<DeploymentDevicePlanEntity>();

    public DbSet<DeploymentOperationEntity> DeploymentOperations => Set<DeploymentOperationEntity>();

    public DbSet<DeploymentDeviceStateEntity> DeploymentDeviceStates => Set<DeploymentDeviceStateEntity>();

    public DbSet<DeploymentLockEntity> DeploymentLocks => Set<DeploymentLockEntity>();

    public DbSet<DeploymentStepEntity> DeploymentSteps => Set<DeploymentStepEntity>();

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

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DriftEventEntity> entry
                 in ChangeTracker.Entries<DriftEventEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "drift_events is append-only: update and delete are not allowed through the application DbContext.");
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

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<OnboardingPlanEntity> entry
                 in ChangeTracker.Entries<OnboardingPlanEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "onboarding_plans is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<OnboardingDevicePlanEntity> entry
                 in ChangeTracker.Entries<OnboardingDevicePlanEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "onboarding_device_plans is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<OnboardingAnchorPlacementEntity> entry
                 in ChangeTracker.Entries<OnboardingAnchorPlacementEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "onboarding_anchor_placements is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<OnboardingOperationEntity> entry
                 in ChangeTracker.Entries<OnboardingOperationEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "onboarding_operations cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (OnboardingOperationEntity.IsTerminal(entry.Property(e => e.State).OriginalValue))
            {
                throw new InvalidOperationException(
                    "Terminal onboarding_operations are immutable and cannot be updated through the application DbContext.");
            }

            if (entry.Property(e => e.NodeId).IsModified
                || entry.Property(e => e.PlanId).IsModified
                || entry.Property(e => e.CreatedBy).IsModified
                || entry.Property(e => e.CreatedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Onboarding operation identity cannot be updated through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<OnboardingStepEntity> entry
                 in ChangeTracker.Entries<OnboardingStepEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "onboarding_steps cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (OnboardingStepEntity.IsTerminal(entry.Property(e => e.State).OriginalValue))
            {
                throw new InvalidOperationException(
                    "Verified/failed onboarding_steps are frozen and cannot be updated through the application DbContext.");
            }

            if (entry.Property(e => e.OperationId).IsModified
                || entry.Property(e => e.DeviceId).IsModified
                || entry.Property(e => e.Sequence).IsModified
                || entry.Property(e => e.Kind).IsModified
                || entry.Property(e => e.ExpectedBeforeHash).IsModified
                || entry.Property(e => e.DesiredAfterHash).IsModified
                || entry.Property(e => e.CreatedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Onboarding step identity and hashes cannot be updated through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DeploymentPlanEntity> entry
                 in ChangeTracker.Entries<DeploymentPlanEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "deployment_plans is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DeploymentDevicePlanEntity> entry
                 in ChangeTracker.Entries<DeploymentDevicePlanEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "deployment_device_plans is append-only: update and delete are not allowed through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DeploymentOperationEntity> entry
                 in ChangeTracker.Entries<DeploymentOperationEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "deployment_operations cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (DeploymentOperationEntity.IsTerminal(entry.Property(e => e.State).OriginalValue))
            {
                throw new InvalidOperationException(
                    "Terminal deployment_operations are immutable and cannot be updated through the application DbContext.");
            }

            if (entry.Property(e => e.NodeId).IsModified
                || entry.Property(e => e.PlanId).IsModified
                || entry.Property(e => e.CreatedBy).IsModified
                || entry.Property(e => e.CreatedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Deployment operation identity cannot be updated through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DeploymentDeviceStateEntity> entry
                 in ChangeTracker.Entries<DeploymentDeviceStateEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "deployment_device_states cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (DeploymentDeviceStateEntity.IsTerminal(entry.Property(e => e.State).OriginalValue))
            {
                throw new InvalidOperationException(
                    "Terminal deployment_device_states are immutable and cannot be updated through the application DbContext.");
            }

            if (entry.Property(e => e.OperationId).IsModified || entry.Property(e => e.DeviceId).IsModified)
            {
                throw new InvalidOperationException(
                    "Deployment device-state identity cannot be updated through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DeploymentLockEntity> entry
                 in ChangeTracker.Entries<DeploymentLockEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "deployment_locks cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (entry.Property(e => e.NodeId).IsModified
                || entry.Property(e => e.DeploymentId).IsModified
                || entry.Property(e => e.OwnerInstanceId).IsModified
                || entry.Property(e => e.AcquiredAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Deployment lock identity cannot be updated through the application DbContext.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DeploymentStepEntity> entry
                 in ChangeTracker.Entries<DeploymentStepEntity>())
        {
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "deployment_steps cannot be deleted through the application DbContext.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            if (DeploymentStepEntity.IsTerminal(entry.Property(e => e.State).OriginalValue))
            {
                throw new InvalidOperationException(
                    "Verified/failed deployment_steps are frozen and cannot be updated through the application DbContext.");
            }

            if (entry.Property(e => e.OperationId).IsModified
                || entry.Property(e => e.DeviceId).IsModified
                || entry.Property(e => e.Sequence).IsModified
                || entry.Property(e => e.Kind).IsModified
                || entry.Property(e => e.ExpectedBeforeHash).IsModified
                || entry.Property(e => e.DesiredAfterHash).IsModified
                || entry.Property(e => e.CreatedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Deployment step identity and hashes cannot be updated through the application DbContext.");
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
