using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class NodeZoneBindingConfiguration : IEntityTypeConfiguration<NodeZoneBindingEntity>
{
    public void Configure(EntityTypeBuilder<NodeZoneBindingEntity> builder)
    {
        builder.ToTable("node_zone_bindings", table =>
        {
            table.HasCheckConstraint("ck_node_zone_bindings_kind", "\"Kind\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_node_zone_bindings_row_version", "\"RowVersion\" > 0");
            table.HasCheckConstraint(
                "ck_node_zone_bindings_expected_hash",
                "octet_length(\"ExpectedDependencyHash\") = 32");
            table.HasCheckConstraint(
                "ck_node_zone_bindings_last_hash",
                "\"LastResolvedDependencyHash\" IS NULL OR octet_length(\"LastResolvedDependencyHash\") = 32");
            table.HasCheckConstraint(
                "ck_node_zone_bindings_values",
                "length(btrim(\"ValuesJson\")) > 2");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.ZoneId).IsRequired();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.ValuesJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.ExpectedDependencyHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.LastResolvedDependencyHash).HasColumnType("bytea");
        builder.Property(e => e.AnalysisStale).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.NodeId, e.ZoneId })
            .IsUnique()
            .HasDatabaseName("uq_node_zone_bindings_node_zone");
        builder.HasIndex(e => e.ZoneId).HasDatabaseName("ix_node_zone_bindings_zone");
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ZoneDefinitionEntity>()
            .WithMany()
            .HasForeignKey(e => e.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
