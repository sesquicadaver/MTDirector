using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingLockConfiguration : IEntityTypeConfiguration<OnboardingLockEntity>
{
    public void Configure(EntityTypeBuilder<OnboardingLockEntity> builder)
    {
        builder.ToTable("onboarding_locks", table =>
        {
            table.HasCheckConstraint("ck_onboarding_locks_owner", "length(btrim(\"OwnerInstanceId\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_onboarding_locks_expiry", "\"ExpiresAtUtc\" >= \"AcquiredAtUtc\"");
        });
        builder.HasKey(e => e.NodeId);
        builder.Property(e => e.NodeId).ValueGeneratedNever();
        builder.Property(e => e.OperationId).IsRequired();
        builder.Property(e => e.OwnerInstanceId).HasColumnType("text").IsRequired();
        builder.Property(e => e.AcquiredAtUtc).IsRequired();
        builder.Property(e => e.HeartbeatAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc).IsRequired();
        builder.HasIndex(e => e.NodeId)
            .IsUnique()
            .HasDatabaseName("uq_onboarding_locks_node");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OnboardingOperationEntity>()
            .WithMany()
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
