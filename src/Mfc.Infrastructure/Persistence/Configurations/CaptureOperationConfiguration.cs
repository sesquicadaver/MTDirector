using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class CaptureOperationConfiguration : IEntityTypeConfiguration<CaptureOperationEntity>
{
    public void Configure(EntityTypeBuilder<CaptureOperationEntity> builder)
    {
        builder.ToTable("capture_operations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.TargetType).IsRequired();
        builder.Property(e => e.TargetId).IsRequired();
        builder.Property(e => e.RequestedBy).IsRequired();
        builder.Property(e => e.IdempotencyKey).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ErrorCode).HasColumnType("text");
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.RequestedBy, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("uq_capture_operation_idempotency");
    }
}
