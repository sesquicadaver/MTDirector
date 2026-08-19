using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentStepConfiguration : IEntityTypeConfiguration<DeploymentStepEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentStepEntity> builder)
    {
        builder.ToTable("deployment_steps", table =>
        {
            table.HasCheckConstraint("ck_deployment_steps_sequence", "\"Sequence\" > 0");
            table.HasCheckConstraint("ck_deployment_steps_kind", "\"Kind\" BETWEEN 0 AND 10");
            table.HasCheckConstraint("ck_deployment_steps_state", "\"State\" BETWEEN 0 AND 3");
            table.HasCheckConstraint("ck_deployment_steps_before_hash", "octet_length(\"ExpectedBeforeHash\") = 32");
            table.HasCheckConstraint("ck_deployment_steps_after_hash", "octet_length(\"DesiredAfterHash\") = 32");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.OperationId).IsRequired();
        builder.Property(e => e.DeviceId).IsRequired();
        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.ExpectedBeforeHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.DesiredAfterHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.State).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.SanitizedError).HasColumnType("text");
        builder.HasIndex(e => new { e.OperationId, e.Sequence })
            .IsUnique()
            .HasDatabaseName("uq_deployment_steps_operation_sequence");
        builder.HasOne<DeploymentOperationEntity>()
            .WithMany()
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
