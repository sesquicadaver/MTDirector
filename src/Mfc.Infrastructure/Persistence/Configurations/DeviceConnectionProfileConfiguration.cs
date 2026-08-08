using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConnectionProfileConfiguration : IEntityTypeConfiguration<DeviceConnectionProfileEntity>
{
    public void Configure(EntityTypeBuilder<DeviceConnectionProfileEntity> builder)
    {
        builder.ToTable("device_connection_profiles", table =>
        {
            table.HasCheckConstraint("ck_connection_username", "length(\"Username\") BETWEEN 1 AND 64");
            table.HasCheckConstraint(
                "ck_connection_spki",
                "\"PinnedSpkiSha256\" IS NULL OR octet_length(\"PinnedSpkiSha256\") = 32");
            table.HasCheckConstraint(
                "ck_connection_connect_timeout",
                "\"ConnectTimeoutMs\" BETWEEN 1000 AND 30000");
            table.HasCheckConstraint(
                "ck_connection_command_timeout",
                "\"CommandTimeoutMs\" BETWEEN 1000 AND 120000");
            table.HasCheckConstraint(
                "ck_connection_max_response",
                "\"MaxResponseBytes\" BETWEEN 1048576 AND 268435456");
            table.HasCheckConstraint("ck_connection_row_version", "\"RowVersion\" > 0");
        });
        builder.HasKey(e => e.DeviceId);
        builder.Property(e => e.DeviceId).ValueGeneratedNever();
        builder.Property(e => e.Username).HasColumnType("text").IsRequired();
        builder.Property(e => e.PinnedSpkiSha256).HasColumnType("bytea");
        builder.Property(e => e.CaProfileRef).HasColumnType("text");
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasOne<DeviceEntity>()
            .WithOne()
            .HasForeignKey<DeviceConnectionProfileEntity>(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EncryptedSecretEntity>()
            .WithMany()
            .HasForeignKey(e => e.EncryptedSecretId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
