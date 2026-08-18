using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class FilterArtifactConfiguration : IEntityTypeConfiguration<FilterArtifactEntity>
{
    public void Configure(EntityTypeBuilder<FilterArtifactEntity> builder)
    {
        builder.ToTable("filter_artifacts", table =>
        {
            table.HasCheckConstraint("ck_filter_artifact_resource_hash", "octet_length(\"ResourceHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_physical_semantics_hash",
                "octet_length(\"PhysicalSemanticsHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_compiler_profile_hash",
                "octet_length(\"CompilerProfileHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_logical_effective_hash",
                "octet_length(\"LogicalEffectivePolicyHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_device_resolved_hash",
                "octet_length(\"DeviceResolvedPolicyHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_analysis_bundle_hash",
                "octet_length(\"AnalysisBundleHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_capability_hash",
                "octet_length(\"CapabilityHash\") = 32");
            table.HasCheckConstraint(
                "ck_filter_artifact_size",
                "\"UncompressedSize\" > 0 AND \"UncompressedSize\" <= 33554432");
            table.HasCheckConstraint(
                "ck_filter_artifact_id",
                "char_length(\"ArtifactId\") = 16");
        });
        builder.HasKey(e => e.ResourceHash);
        builder.Property(e => e.ResourceHash).HasColumnType("bytea").ValueGeneratedNever();
        builder.Property(e => e.ArtifactId).HasMaxLength(16).IsRequired();
        builder.Property(e => e.DeviceId).IsRequired();
        builder.Property(e => e.PhysicalSemanticsHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.CompilerProfileHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.LogicalEffectivePolicyHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.DeviceResolvedPolicyHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.AnalysisBundleHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.CapabilityHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.CompilerVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CompiledAtUtc).IsRequired();
        builder.Property(e => e.Compression).IsRequired();
        builder.Property(e => e.UncompressedSize).IsRequired();
        builder.Property(e => e.CompressedPayload).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => e.DeviceId);
        builder.HasIndex(e => e.ArtifactId);
    }
}
