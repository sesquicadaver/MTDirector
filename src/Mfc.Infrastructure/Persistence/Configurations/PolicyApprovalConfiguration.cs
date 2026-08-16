using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class PolicyApprovalConfiguration : IEntityTypeConfiguration<PolicyApprovalEntity>
{
    public void Configure(EntityTypeBuilder<PolicyApprovalEntity> builder)
    {
        builder.ToTable("policy_approvals", table =>
        {
            table.HasCheckConstraint("ck_policy_approvals_bundle_hash", "octet_length(\"BundleHash\") = 32");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.RevisionId).IsRequired();
        builder.Property(e => e.AnalysisRunId).IsRequired();
        builder.Property(e => e.BundleHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ReviewerId).IsRequired();
        builder.Property(e => e.IsSecurityOwner).IsRequired();
        builder.Property(e => e.RecordedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.RevisionId, e.ReviewerId, e.AnalysisRunId })
            .IsUnique()
            .HasDatabaseName("uq_policy_approvals_revision_reviewer_run");
        builder.HasOne<PolicyRevisionEntity>()
            .WithMany()
            .HasForeignKey(e => e.RevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PolicyAnalysisRunEntity>()
            .WithMany()
            .HasForeignKey(e => e.AnalysisRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
