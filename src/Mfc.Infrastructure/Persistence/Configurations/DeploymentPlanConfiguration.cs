using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentPlanConfiguration : IEntityTypeConfiguration<DeploymentPlanEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentPlanEntity> builder)
    {
        builder.ToTable("deployment_plans", table =>
        {
            table.HasCheckConstraint("ck_deployment_plans_policy_hash", "octet_length(\"LogicalPolicyHash\") = 32");
            table.HasCheckConstraint("ck_deployment_plans_analysis_hash", "octet_length(\"AnalysisBundleHash\") = 32");
            table.HasCheckConstraint("ck_deployment_plans_topology_hash", "octet_length(\"TopologyProjectionHash\") = 32");
            table.HasCheckConstraint("ck_deployment_plans_plan_hash", "octet_length(\"PlanHash\") = 32");
            table.HasCheckConstraint("ck_deployment_plans_lifetime", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
            table.HasCheckConstraint("ck_deployment_plans_activation", "length(btrim(\"ActivationOrderJson\")) >= 2");
            table.HasCheckConstraint("ck_deployment_plans_rollback", "length(btrim(\"RollbackOrderJson\")) >= 2");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.LogicalPolicyHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.AnalysisBundleHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.TopologyProjectionHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ActivationOrderJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.RollbackOrderJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc).IsRequired();
        builder.Property(e => e.PlanHash).HasColumnType("bytea").IsRequired();
        builder.HasIndex(e => e.NodeId).HasDatabaseName("ix_deployment_plans_node");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.DevicePlans)
            .WithOne(e => e.Plan)
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
