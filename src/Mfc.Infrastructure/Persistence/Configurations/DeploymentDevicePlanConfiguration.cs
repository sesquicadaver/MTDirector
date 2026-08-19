using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentDevicePlanConfiguration : IEntityTypeConfiguration<DeploymentDevicePlanEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentDevicePlanEntity> builder)
    {
        builder.ToTable("deployment_device_plans", table =>
        {
            table.HasCheckConstraint("ck_deployment_device_plans_version", "length(btrim(\"ExpectedRouterOsVersion\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_deployment_device_plans_cap_hash", "octet_length(\"ExpectedCapabilityHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_cfg_hash", "octet_length(\"ExpectedConfigurationHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_compat_hash", "octet_length(\"ExpectedCompatibilityHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_guard_hash", "octet_length(\"ExpectedGuardContextHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_anchor_hash", "octet_length(\"ExpectedAnchorContextHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_old_art", "octet_length(\"OldArtifactHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_new_art", "octet_length(\"NewArtifactHash\") = 32");
            table.HasCheckConstraint("ck_deployment_device_plans_old_targets", "length(btrim(\"OldAnchorTargetsJson\")) >= 2");
            table.HasCheckConstraint("ck_deployment_device_plans_new_targets", "length(btrim(\"NewAnchorTargetsJson\")) >= 2");
            table.HasCheckConstraint("ck_deployment_device_plans_act_order", "length(btrim(\"AnchorActivationOrderJson\")) >= 2");
            table.HasCheckConstraint("ck_deployment_device_plans_rb_order", "length(btrim(\"AnchorRollbackOrderJson\")) >= 2");
            table.HasCheckConstraint("ck_deployment_device_plans_transitions", "length(btrim(\"TransitionStateHashesJson\")) >= 2");
            table.HasCheckConstraint("ck_deployment_device_plans_probes", "length(btrim(\"ProbesJson\")) >= 2");
            table.HasCheckConstraint(
                "ck_deployment_device_plans_rollback_ttl",
                "\"RollbackTtlSeconds\" BETWEEN 60 AND 600");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.PlanId).IsRequired();
        builder.Property(e => e.DeviceId).IsRequired();
        builder.Property(e => e.ExpectedRouterOsVersion).HasColumnType("text").IsRequired();
        builder.Property(e => e.ExpectedCapabilityHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedConfigurationHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedCompatibilityHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedGuardContextHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedAnchorContextHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.OldArtifactHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.NewArtifactHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.OldAnchorTargetsJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.NewAnchorTargetsJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.AnchorActivationOrderJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.AnchorRollbackOrderJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.TransitionStateHashesJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.RollbackTtlSeconds).IsRequired();
        builder.Property(e => e.ProbesJson).HasColumnType("text").IsRequired();
        builder.HasIndex(e => new { e.PlanId, e.DeviceId })
            .IsUnique()
            .HasDatabaseName("uq_deployment_device_plans_plan_device");
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
