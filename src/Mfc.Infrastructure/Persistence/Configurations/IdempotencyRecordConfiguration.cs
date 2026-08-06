using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecordEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecordEntity> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Actor).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Operation).HasMaxLength(128).IsRequired();
        builder.Property(e => e.RequestHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ResponseRef).HasMaxLength(512);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.Actor, e.Operation, e.Key }).IsUnique();
    }
}
