using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class ZoneDefinitionConfiguration : IEntityTypeConfiguration<ZoneDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<ZoneDefinitionEntity> builder)
    {
        builder.ToTable("zone_definitions", table =>
        {
            table.HasCheckConstraint("ck_zone_definitions_key", "length(btrim(\"Key\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_zone_definitions_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_zone_definitions_owner_scope", "\"OwnerScope\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_zone_definitions_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint(
                "ck_zone_definitions_owner_rules",
                """
                (
                  ("OwnerScope" = 0 AND "OwnerId" IS NULL)
                  OR ("OwnerScope" IN (1, 2) AND "OwnerId" IS NOT NULL)
                )
                """);
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.OwnerScope).IsRequired();
        builder.Property(e => e.OwnerId);
        builder.Property(e => e.Key).HasColumnType("text").IsRequired();
        builder.Property(e => e.Name).HasColumnType("text").IsRequired();
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.OwnerScope, e.OwnerId, e.Key })
            .IsUnique()
            .HasDatabaseName("uq_zone_definitions_owner_key");
    }
}
