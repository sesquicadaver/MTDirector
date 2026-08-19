using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentOperationConfiguration : IEntityTypeConfiguration<DeploymentOperationEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentOperationEntity> builder)
    {
        builder.ToTable("deployment_operations", table =>
        {
            table.HasCheckConstraint("ck_deployment_operations_state", "\"State\" BETWEEN 0 AND 17");
            table.HasCheckConstraint("ck_deployment_operations_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint(
                "ck_deployment_operations_terminal_completed",
                "(\"State\" IN (9, 12, 13, 14, 15, 16, 17) AND \"CompletedAtUtc\" IS NOT NULL) OR (\"State\" NOT IN (9, 12, 13, 14, 15, 16, 17))");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.PlanId).IsRequired();
        builder.Property(e => e.State).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();
        builder.Property(e => e.ErrorCode).HasColumnType("text");
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => e.NodeId)
            .IsUnique()
            .HasFilter("\"State\" NOT IN (9, 12, 13, 14, 15, 16, 17)")
            .HasDatabaseName("uq_deployment_operations_node_nonterminal");
        builder.HasIndex(e => e.PlanId).HasDatabaseName("ix_deployment_operations_plan");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeploymentPlanEntity>()
            .WithMany()
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
