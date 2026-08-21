using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class RoutingAssuranceStateConfiguration : IEntityTypeConfiguration<RoutingAssuranceStateEntity>
{
    public void Configure(EntityTypeBuilder<RoutingAssuranceStateEntity> builder)
    {
        builder.ToTable("routing_assurance_states", table =>
        {
            table.HasCheckConstraint(
                "ck_routing_assurance_states_config_hash",
                "octet_length(\"ConfigurationHash\") = 32");
            table.HasCheckConstraint(
                "ck_routing_assurance_states_ops_hash",
                "octet_length(\"OperationalHash\") = 32");
            table.HasCheckConstraint("ck_routing_assurance_states_row_version", "\"RowVersion\" > 0");
        });
        builder.HasKey(e => e.DeviceId);
        builder.Property(e => e.DeviceId).ValueGeneratedNever();
        builder.Property(e => e.ConfigurationHash).IsRequired().HasColumnType("bytea");
        builder.Property(e => e.OperationalHash).IsRequired().HasColumnType("bytea");
        builder.Property(e => e.ConfigurationJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.OperationalJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.RouteExpectationsJson).IsRequired().HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");
        builder.Property(e => e.RouteFindingsJson).IsRequired().HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");
        builder.Property(e => e.ResolutionTracesJson).IsRequired().HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.RowVersion).IsRequired().HasDefaultValue(1L);
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
