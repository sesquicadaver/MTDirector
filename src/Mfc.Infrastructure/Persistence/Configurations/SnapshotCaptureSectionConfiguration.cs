using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class SnapshotCaptureSectionConfiguration : IEntityTypeConfiguration<SnapshotCaptureSectionEntity>
{
    public void Configure(EntityTypeBuilder<SnapshotCaptureSectionEntity> builder)
    {
        builder.ToTable("snapshot_capture_sections");
        builder.HasKey(e => new { e.CaptureId, e.SectionId });
        builder.Property(e => e.SectionId).HasColumnType("text").IsRequired();
        builder.Property(e => e.SectionVersion).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Ordered).IsRequired();
        builder.Property(e => e.ConfigurationRecordCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.ObservationRecordCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CapabilityRecordCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CompatibilityRecordCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.RawHash).HasColumnType("bytea");
        builder.Property(e => e.ConfigurationHash).HasColumnType("bytea");
        builder.Property(e => e.ObservationHash).HasColumnType("bytea");
        builder.Property(e => e.CapabilityHash).HasColumnType("bytea");
        builder.Property(e => e.CompatibilityHash).HasColumnType("bytea");

        builder.HasOne<SnapshotCaptureEntity>()
            .WithMany()
            .HasForeignKey(e => e.CaptureId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        // Optional content-addressed FKs (Canonical Spec §28.2): each non-null hash → snapshot_payloads.
        builder.HasOne<SnapshotPayloadEntity>()
            .WithMany()
            .HasForeignKey(e => e.RawHash)
            .HasPrincipalKey(p => p.PayloadHash)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("fk_snapshot_capture_sections_raw_hash");

        builder.HasOne<SnapshotPayloadEntity>()
            .WithMany()
            .HasForeignKey(e => e.ConfigurationHash)
            .HasPrincipalKey(p => p.PayloadHash)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("fk_snapshot_capture_sections_configuration_hash");

        builder.HasOne<SnapshotPayloadEntity>()
            .WithMany()
            .HasForeignKey(e => e.ObservationHash)
            .HasPrincipalKey(p => p.PayloadHash)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("fk_snapshot_capture_sections_observation_hash");

        builder.HasOne<SnapshotPayloadEntity>()
            .WithMany()
            .HasForeignKey(e => e.CapabilityHash)
            .HasPrincipalKey(p => p.PayloadHash)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("fk_snapshot_capture_sections_capability_hash");

        builder.HasOne<SnapshotPayloadEntity>()
            .WithMany()
            .HasForeignKey(e => e.CompatibilityHash)
            .HasPrincipalKey(p => p.PayloadHash)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("fk_snapshot_capture_sections_compatibility_hash");
    }
}
