using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class PolicyBindingConfiguration : IEntityTypeConfiguration<PolicyBindingEntity>
{
    public void Configure(EntityTypeBuilder<PolicyBindingEntity> builder)
    {
        builder.ToTable("policy_bindings", table =>
        {
            table.HasCheckConstraint("ck_policy_bindings_scope", "\"Scope\" BETWEEN 0 AND 3");
            table.HasCheckConstraint("ck_policy_bindings_state", "\"State\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_policy_bindings_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint("ck_policy_bindings_bundle_hash", "octet_length(\"BundleHash\") = 32");
            table.HasCheckConstraint(
                "ck_policy_bindings_scope_id",
                "(\"Scope\" = 0 AND \"ScopeId\" IS NULL) OR (\"Scope\" <> 0 AND \"ScopeId\" IS NOT NULL)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Scope).IsRequired();
        builder.Property(e => e.ScopeId);
        builder.Property(e => e.PolicyId).IsRequired();
        builder.Property(e => e.DesiredRevisionId).IsRequired();
        builder.Property(e => e.AnalysisRunId).IsRequired();
        builder.Property(e => e.BundleHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.State).IsRequired();
        builder.Property(e => e.ValidFromUtc);
        builder.Property(e => e.ValidUntilUtc);
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => e.Scope)
            .IsUnique()
            .HasFilter("\"State\" = 0 AND \"Scope\" = 0")
            .HasDatabaseName("uq_policy_bindings_company_active");
        builder.HasIndex(e => new { e.Scope, e.ScopeId })
            .IsUnique()
            .HasFilter("\"State\" = 0 AND \"Scope\" IN (1, 2)")
            .HasDatabaseName("uq_policy_bindings_overlay_active");
        builder.HasIndex(e => e.PolicyId)
            .IsUnique()
            .HasFilter("\"State\" = 0 AND \"Scope\" = 3")
            .HasDatabaseName("uq_policy_bindings_exception_policy_active");
        builder.HasIndex(e => e.DesiredRevisionId).HasDatabaseName("ix_policy_bindings_revision");
        builder.HasOne<PolicyEntity>()
            .WithMany()
            .HasForeignKey(e => e.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PolicyRevisionEntity>()
            .WithMany()
            .HasForeignKey(e => e.DesiredRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PolicyAnalysisRunEntity>()
            .WithMany()
            .HasForeignKey(e => e.AnalysisRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
