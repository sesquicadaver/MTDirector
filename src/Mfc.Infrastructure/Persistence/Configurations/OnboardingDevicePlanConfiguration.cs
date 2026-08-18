using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingDevicePlanConfiguration : IEntityTypeConfiguration<OnboardingDevicePlanEntity>
{
    public void Configure(EntityTypeBuilder<OnboardingDevicePlanEntity> builder)
    {
        builder.ToTable("onboarding_device_plans", table =>
        {
            table.HasCheckConstraint("ck_onboarding_device_plans_version", "length(btrim(\"ExpectedRouterOsVersion\")) BETWEEN 1 AND 128");
            table.HasCheckConstraint("ck_onboarding_device_plans_cap_hash", "octet_length(\"ExpectedCapabilityHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_cfg_hash", "octet_length(\"ExpectedConfigurationHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_compat_hash", "octet_length(\"ExpectedCompatibilityHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_api_hash", "octet_length(\"ExpectedApiServiceHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_read_hash", "octet_length(\"ExpectedReadAccountHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_deploy_hash", "octet_length(\"ExpectedDeploymentAccountHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_mode_hash", "octet_length(\"ExpectedDeviceModeHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_guard_hash", "octet_length(\"ExpectedGuardHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_bootstrap_hash", "octet_length(\"BootstrapArtifactHash\") = 32");
            table.HasCheckConstraint("ck_onboarding_device_plans_anchors", "length(btrim(\"RequiredAnchorSetJson\")) >= 2");
            table.HasCheckConstraint(
                "ck_onboarding_device_plans_watchdog_ttl",
                "\"WatchdogTtlSeconds\" BETWEEN 60 AND 600");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.PlanId).IsRequired();
        builder.Property(e => e.DeviceId).IsRequired();
        builder.Property(e => e.ExpectedRouterOsVersion).HasColumnType("text").IsRequired();
        builder.Property(e => e.ExpectedCapabilityHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedConfigurationHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedCompatibilityHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedApiServiceHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedReadAccountHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedDeploymentAccountHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedDeviceModeHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ExpectedGuardHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.RequiredAnchorSetJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.BootstrapArtifactHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.WatchdogTtlSeconds).IsRequired();
        builder.HasIndex(e => new { e.PlanId, e.DeviceId })
            .IsUnique()
            .HasDatabaseName("uq_onboarding_device_plans_plan_device");
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Placements)
            .WithOne(e => e.DevicePlan)
            .HasForeignKey(e => e.DevicePlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
