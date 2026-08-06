using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.Actor).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.PreviousEventHash).HasColumnType("bytea");
        builder.Property(e => e.EventHash).HasColumnType("bytea").IsRequired();
        builder.HasIndex(e => e.OccurredAtUtc);
        builder.HasIndex(e => e.EventHash).IsUnique();
    }
}
