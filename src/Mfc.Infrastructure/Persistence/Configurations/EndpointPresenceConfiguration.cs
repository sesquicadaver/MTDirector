using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class EndpointPresenceIntervalConfiguration : IEntityTypeConfiguration<EndpointPresenceIntervalEntity>
{
    public void Configure(EntityTypeBuilder<EndpointPresenceIntervalEntity> builder)
    {
        builder.ToTable("endpoint_presence_intervals", table =>
        {
            table.HasCheckConstraint(
                "ck_endpoint_presence_intervals_validity",
                "\"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
        });
        builder.HasKey(e => e.PresenceId);
        builder.Property(e => e.PresenceId).ValueGeneratedNever();
        builder.Property(e => e.EndpointId).IsRequired();
        builder.Property(e => e.SiteId).IsRequired();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.DeviceId);
        builder.Property(e => e.VlanId).HasMaxLength(64);
        builder.Property(e => e.Vrf).HasMaxLength(128);
        builder.Property(e => e.SourceAddress).IsRequired().HasMaxLength(128);
        builder.Property(e => e.MacAddress).HasMaxLength(64);
        builder.Property(e => e.AttributionCertainty).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidUntil);
        builder.HasIndex(e => e.EndpointId);
        builder.HasIndex(e => new { e.EndpointId, e.ValidFrom });
        builder.HasIndex(e => e.EndpointId)
            .IsUnique()
            .HasFilter("\"ValidUntil\" IS NULL")
            .HasDatabaseName("ux_endpoint_presence_intervals_active_endpoint");
    }
}

internal sealed class EndpointRoutingContextConfiguration : IEntityTypeConfiguration<EndpointRoutingContextEntity>
{
    public void Configure(EntityTypeBuilder<EndpointRoutingContextEntity> builder)
    {
        builder.ToTable("endpoint_routing_contexts", table =>
        {
            table.HasCheckConstraint(
                "ck_endpoint_routing_contexts_validity",
                "\"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
        });
        builder.HasKey(e => e.PresenceId);
        builder.Property(e => e.PresenceId).ValueGeneratedNever();
        builder.Property(e => e.EndpointId).IsRequired();
        builder.Property(e => e.SiteId).IsRequired();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.VlanId).HasMaxLength(64);
        builder.Property(e => e.Vrf).HasMaxLength(128);
        builder.Property(e => e.SourceAddress).IsRequired().HasMaxLength(128);
        builder.Property(e => e.CorporateRouteTraceJson).HasColumnType("jsonb");
        builder.Property(e => e.InternetRouteTraceJson).HasColumnType("jsonb");
        builder.Property(e => e.WazuhRouteTraceJson).HasColumnType("jsonb");
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidUntil);
        builder.HasIndex(e => e.EndpointId);
        builder.HasOne<EndpointPresenceIntervalEntity>()
            .WithMany()
            .HasForeignKey(e => e.PresenceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
