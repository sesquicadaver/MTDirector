using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class PolicyRevisionConfiguration : IEntityTypeConfiguration<PolicyRevisionEntity>
{
    public void Configure(EntityTypeBuilder<PolicyRevisionEntity> builder)
    {
        builder.ToTable("policy_revisions", table =>
        {
            table.HasCheckConstraint("ck_policy_revisions_revision_number", "\"RevisionNumber\" > 0");
            table.HasCheckConstraint("ck_policy_revisions_schema_version", "\"SchemaVersion\" > 0");
            table.HasCheckConstraint("ck_policy_revisions_state", "\"State\" BETWEEN 0 AND 6");
            table.HasCheckConstraint("ck_policy_revisions_content_hash", "octet_length(\"ContentHash\") = 32");
            table.HasCheckConstraint(
                "ck_policy_revisions_parent_hash",
                "\"ParentContextHash\" IS NULL OR octet_length(\"ParentContextHash\") = 32");
            table.HasCheckConstraint(
                "ck_policy_revisions_size",
                "\"UncompressedSize\" > 0 AND \"UncompressedSize\" <= 268435456");
            table.HasCheckConstraint(
                "ck_policy_revisions_approved_at",
                "(\"State\" = 3 AND \"ApprovedAtUtc\" IS NOT NULL) OR (\"State\" <> 3)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.PolicyId).IsRequired();
        builder.Property(e => e.RevisionNumber).IsRequired();
        builder.Property(e => e.SchemaVersion).IsRequired();
        builder.Property(e => e.ContentHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ParentContextHash).HasColumnType("bytea");
        builder.Property(e => e.State).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ApprovedAtUtc);
        builder.Property(e => e.Compression).IsRequired();
        builder.Property(e => e.UncompressedSize).IsRequired();
        builder.Property(e => e.CompressedPayload).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.PolicyId, e.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("uq_policy_revisions_policy_revision");
        builder.HasIndex(e => e.ContentHash).HasDatabaseName("ix_policy_revisions_content_hash");
        builder.HasOne<PolicyEntity>()
            .WithMany()
            .HasForeignKey(e => e.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
