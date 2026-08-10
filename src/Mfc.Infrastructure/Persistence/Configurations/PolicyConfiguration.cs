using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<PolicyEntity>
{
    public void Configure(EntityTypeBuilder<PolicyEntity> builder)
    {
        builder.ToTable("policies", table =>
        {
            table.HasCheckConstraint("ck_policies_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_policies_kind", "\"Kind\" BETWEEN 0 AND 3");
            table.HasCheckConstraint("ck_policies_owner_scope", "\"OwnerScope\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_policies_status", "\"Status\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("ck_policies_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint(
                "ck_policies_owner_rules",
                """
                (
                  ("Kind" = 0 AND "OwnerScope" = 0 AND "OwnerId" IS NULL)
                  OR ("Kind" = 1 AND "OwnerScope" = 1 AND "OwnerId" IS NOT NULL)
                  OR ("Kind" = 2 AND "OwnerScope" = 2 AND "OwnerId" IS NOT NULL)
                  OR ("Kind" = 3 AND "OwnerScope" IN (1, 2) AND "OwnerId" IS NOT NULL)
                )
                """);
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnType("text").IsRequired();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.OwnerScope).IsRequired();
        builder.Property(e => e.OwnerId);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.Kind, e.OwnerId }).HasDatabaseName("ix_policies_kind_owner");
    }
}
