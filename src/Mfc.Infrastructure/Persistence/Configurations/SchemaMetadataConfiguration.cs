using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mfc.Infrastructure.Persistence.Entities;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class SchemaMetadataConfiguration : IEntityTypeConfiguration<SchemaMetadataEntity>
{
    public void Configure(EntityTypeBuilder<SchemaMetadataEntity> builder)
    {
        builder.ToTable("schema_metadata");
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
    }
}
