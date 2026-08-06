using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class ControllerInstanceConfiguration : IEntityTypeConfiguration<ControllerInstanceEntity>
{
    public void Configure(EntityTypeBuilder<ControllerInstanceEntity> builder)
    {
        builder.ToTable("controller_instances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.HostName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ApplicationVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.StartedAtUtc).IsRequired();
        builder.Property(e => e.LastSeenAtUtc).IsRequired();
        builder.HasIndex(e => e.HostName);
    }
}
