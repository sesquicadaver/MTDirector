using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentLockConfiguration : IEntityTypeConfiguration<DeploymentLockEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentLockEntity> builder)
    {
        builder.ToTable("deployment_locks", table =>
        {
            table.HasCheckConstraint("ck_deployment_locks_owner", "length(btrim(\"OwnerInstanceId\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_deployment_locks_expiry", "\"ExpiresAtUtc\" > \"AcquiredAtUtc\"");
        });
        builder.HasKey(e => e.NodeId);
        builder.Property(e => e.NodeId).ValueGeneratedNever();
        builder.Property(e => e.DeploymentId).IsRequired();
        builder.Property(e => e.OwnerInstanceId).HasColumnType("text").IsRequired();
        builder.Property(e => e.AcquiredAtUtc).IsRequired();
        builder.Property(e => e.HeartbeatAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc).IsRequired();
        builder.HasIndex(e => e.NodeId)
            .IsUnique()
            .HasDatabaseName("uq_deployment_locks_node");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeploymentOperationEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeploymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
