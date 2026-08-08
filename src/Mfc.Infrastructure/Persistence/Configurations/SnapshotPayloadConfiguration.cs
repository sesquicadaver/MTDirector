using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class SnapshotPayloadConfiguration : IEntityTypeConfiguration<SnapshotPayloadEntity>
{
    public void Configure(EntityTypeBuilder<SnapshotPayloadEntity> builder)
    {
        builder.ToTable("snapshot_payloads", table =>
        {
            table.HasCheckConstraint("ck_snapshot_payload_hash", "octet_length(\"PayloadHash\") = 32");
            table.HasCheckConstraint(
                "ck_snapshot_payload_size",
                "\"UncompressedSize\" > 0 AND \"UncompressedSize\" <= 268435456");
        });
        builder.HasKey(e => e.PayloadHash);
        builder.Property(e => e.PayloadHash).HasColumnType("bytea").ValueGeneratedNever();
        builder.Property(e => e.PayloadKind).IsRequired();
        builder.Property(e => e.SchemaVersion).IsRequired();
        builder.Property(e => e.Compression).IsRequired();
        builder.Property(e => e.UncompressedSize).IsRequired();
        builder.Property(e => e.CompressedPayload).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
    }
}
