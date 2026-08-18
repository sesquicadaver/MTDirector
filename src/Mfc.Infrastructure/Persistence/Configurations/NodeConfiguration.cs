using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class NodeConfiguration : IEntityTypeConfiguration<NodeEntity>
{
    public void Configure(EntityTypeBuilder<NodeEntity> builder)
    {
        builder.ToTable("nodes", table =>
        {
            table.HasCheckConstraint("ck_nodes_name", "length(btrim(\"Name\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_nodes_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint("ck_nodes_management_state", "\"ManagementState\" BETWEEN 0 AND 2");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnType("text").IsRequired();
        builder.Property(e => e.DeclaredKind).IsRequired();
        builder.Property(e => e.DeclaredUplinkMode).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ManagementState).IsRequired().HasDefaultValue((short)0);
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.SiteId, e.Name }).IsUnique().HasDatabaseName("uq_nodes_site_name");
        builder.HasOne<SiteEntity>()
            .WithMany()
            .HasForeignKey(e => e.SiteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
