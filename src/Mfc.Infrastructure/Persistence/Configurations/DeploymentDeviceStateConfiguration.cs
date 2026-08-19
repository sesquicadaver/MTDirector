using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentDeviceStateConfiguration : IEntityTypeConfiguration<DeploymentDeviceStateEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentDeviceStateEntity> builder)
    {
        builder.ToTable("deployment_device_states", table =>
        {
            table.HasCheckConstraint("ck_deployment_device_states_state", "\"State\" BETWEEN 0 AND 12");
        });
        builder.HasKey(e => new { e.OperationId, e.DeviceId });
        builder.Property(e => e.State).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasOne<DeploymentOperationEntity>()
            .WithMany()
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
