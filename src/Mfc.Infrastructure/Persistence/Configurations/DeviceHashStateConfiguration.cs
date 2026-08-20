using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeviceHashStateConfiguration : IEntityTypeConfiguration<DeviceHashStateEntity>
{
    public void Configure(EntityTypeBuilder<DeviceHashStateEntity> builder)
    {
        builder.ToTable("device_hash_states", table =>
        {
            table.HasCheckConstraint(
                "ck_device_hash_states_desired_policy",
                "\"DesiredPolicyHash\" IS NULL OR octet_length(\"DesiredPolicyHash\") = 32");
            table.HasCheckConstraint(
                "ck_device_hash_states_desired_artifact",
                "\"DesiredArtifactHash\" IS NULL OR octet_length(\"DesiredArtifactHash\") = 32");
            table.HasCheckConstraint(
                "ck_device_hash_states_committed_policy",
                "\"LastCommittedPolicyHash\" IS NULL OR octet_length(\"LastCommittedPolicyHash\") = 32");
            table.HasCheckConstraint(
                "ck_device_hash_states_committed_artifact",
                "\"LastCommittedArtifactHash\" IS NULL OR octet_length(\"LastCommittedArtifactHash\") = 32");
            table.HasCheckConstraint(
                "ck_device_hash_states_actual",
                "\"ActualManagedResourceHash\" IS NULL OR octet_length(\"ActualManagedResourceHash\") = 32");
            table.HasCheckConstraint("ck_device_hash_states_row_version", "\"RowVersion\" > 0");
        });
        builder.HasKey(e => e.DeviceId);
        builder.Property(e => e.DeviceId).ValueGeneratedNever();
        builder.Property(e => e.DesiredPolicyHash).HasColumnType("bytea");
        builder.Property(e => e.DesiredArtifactHash).HasColumnType("bytea");
        builder.Property(e => e.LastCommittedPolicyHash).HasColumnType("bytea");
        builder.Property(e => e.LastCommittedArtifactHash).HasColumnType("bytea");
        builder.Property(e => e.ActualManagedResourceHash).HasColumnType("bytea");
        builder.Property(e => e.ActualKnown).IsRequired();
        builder.Property(e => e.AnchorKnown).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
