using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class SnapshotCaptureConfiguration : IEntityTypeConfiguration<SnapshotCaptureEntity>
{
    public void Configure(EntityTypeBuilder<SnapshotCaptureEntity> builder)
    {
        builder.ToTable("snapshot_captures");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.AttemptCount).IsRequired();
        builder.Property(e => e.CaptureStartedAtUtc).IsRequired();
        builder.Property(e => e.RawPayloadHash).HasColumnType("bytea");
        builder.Property(e => e.ConfigurationPayloadHash).HasColumnType("bytea");
        builder.Property(e => e.ObservationPayloadHash).HasColumnType("bytea");
        builder.Property(e => e.CapabilityPayloadHash).HasColumnType("bytea");
        builder.Property(e => e.CompatibilityPayloadHash).HasColumnType("bytea");
        builder.Property(e => e.ConfigurationHash).HasColumnType("bytea");
        builder.Property(e => e.ObservationHash).HasColumnType("bytea");
        builder.Property(e => e.CapabilityHash).HasColumnType("bytea");
        builder.Property(e => e.CompatibilityMaterialHash).HasColumnType("bytea");
        builder.Property(e => e.SnapshotHash).HasColumnType("bytea");
        builder.Property(e => e.SectionResultsJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();
        builder.Property(e => e.ErrorCode).HasColumnType("text");
        builder.Property(e => e.ErrorDetailsJson).HasColumnType("jsonb");
        builder.HasIndex(e => new { e.OperationId, e.DeviceId })
            .IsUnique()
            .HasDatabaseName("uq_snapshot_capture_device_operation");
        builder.HasOne<CaptureOperationEntity>()
            .WithMany()
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
