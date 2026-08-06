using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class EncryptedSecretConfiguration : IEntityTypeConfiguration<EncryptedSecretEntity>
{
    public void Configure(EntityTypeBuilder<EncryptedSecretEntity> builder)
    {
        builder.ToTable("encrypted_secrets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Ciphertext).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.WrappedDek).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.Algorithm).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.RotatedAtUtc);
    }
}
