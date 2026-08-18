using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<DeviceEntity>
{
    public void Configure(EntityTypeBuilder<DeviceEntity> builder)
    {
        builder.ToTable("devices", table =>
        {
            table.HasCheckConstraint("ck_devices_name", "length(btrim(\"DisplayName\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_devices_port", "\"ManagementPort\" BETWEEN 1 AND 65535");
            table.HasCheckConstraint("ck_devices_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint("ck_devices_management_state", "\"ManagementState\" BETWEEN 0 AND 2");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.DisplayName).HasColumnType("text").IsRequired();
        builder.Property(e => e.ManagementHost).HasColumnType("text").IsRequired();
        builder.Property(e => e.ManagementHostKind).IsRequired();
        builder.Property(e => e.ManagementPort).IsRequired().HasDefaultValue(8729);
        builder.Property(e => e.Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Role).IsRequired().HasDefaultValue((short)0);
        builder.Property(e => e.ManagementState).IsRequired().HasDefaultValue((short)0);
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.ManagementHost, e.ManagementPort })
            .IsUnique()
            .HasFilter("\"Enabled\" = TRUE")
            .HasDatabaseName("uq_devices_active_endpoint");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
