using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingPlanConfiguration : IEntityTypeConfiguration<OnboardingPlanEntity>
{
    public void Configure(EntityTypeBuilder<OnboardingPlanEntity> builder)
    {
        builder.ToTable("onboarding_plans", table =>
        {
            table.HasCheckConstraint("ck_onboarding_plans_membership_hash", "octet_length(\"NodeMembershipHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_plans_topology_hash", "octet_length(\"TopologyProjectionHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_plans_plan_hash", "octet_length(\"PlanHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_plans_lifetime", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.NodeMembershipHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.TopologyProjectionHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc).IsRequired();
        builder.Property(e => e.PlanHash).HasColumnType("bytea").IsRequired();
        builder.HasIndex(e => e.NodeId).HasDatabaseName("ix_onboarding_plans_node");
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
