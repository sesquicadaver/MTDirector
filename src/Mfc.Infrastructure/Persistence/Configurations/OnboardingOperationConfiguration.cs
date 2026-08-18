using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingOperationConfiguration : IEntityTypeConfiguration<OnboardingOperationEntity>
{
    public void Configure(EntityTypeBuilder<OnboardingOperationEntity> builder)
    {
        builder.ToTable("onboarding_operations", table =>
        {
            table.HasCheckConstraint("ck_onboarding_operations_state", "\"State\" BETWEEN 0 AND 13");
            table.HasCheckConstraint("ck_onboarding_operations_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint(
                "ck_onboarding_operations_terminal_completed",
                "(\"State\" IN (8, 11, 12, 13) AND \"CompletedAtUtc\" IS NOT NULL) OR (\"State\" NOT IN (8, 11, 12, 13))");
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
            .HasFilter("\"State\" NOT IN (8, 11, 12, 13)")
            .HasDatabaseName("uq_onboarding_operations_node_nonterminal");
        builder.HasIndex(e => e.PlanId).HasDatabaseName("ix_onboarding_operations_plan");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OnboardingPlanEntity>()
            .WithMany()
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
