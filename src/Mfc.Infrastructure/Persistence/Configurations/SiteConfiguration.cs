using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<SiteEntity>
{
    public void Configure(EntityTypeBuilder<SiteEntity> builder)
    {
        builder.ToTable("sites", table =>
        {
            table.HasCheckConstraint("ck_sites_code", "\"Code\" ~ '^[A-Z][A-Z0-9_-]{1,31}$'");
            table.HasCheckConstraint("ck_sites_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_sites_row_version", "\"RowVersion\" > 0");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnType("text").IsRequired();
        builder.Property(e => e.Name).HasColumnType("text").IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("uq_sites_code");
    }
}
