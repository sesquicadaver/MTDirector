using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class PolicyAnalysisRunConfiguration : IEntityTypeConfiguration<PolicyAnalysisRunEntity>
{
    public void Configure(EntityTypeBuilder<PolicyAnalysisRunEntity> builder)
    {
        builder.ToTable("policy_analysis_runs", table =>
        {
            table.HasCheckConstraint("ck_policy_analysis_runs_revision_hash", "octet_length(\"RevisionContentHash\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_logical_hash", "octet_length(\"LogicalEffectiveHash\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_analysis_hash", "octet_length(\"AnalysisContextHash\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_evidence_hash", "octet_length(\"EvidenceContextHash\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_topology_hash", "octet_length(\"TopologyProjectionHash\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_impact_hash", "octet_length(\"ImpactSetHash\") = 32");
            table.HasCheckConstraint(
                "ck_policy_analysis_runs_device_hashes",
                "octet_length(\"PerDeviceAnalysisHashes\") % 32 = 0");
            table.HasCheckConstraint("ck_policy_analysis_runs_bundle_hash", "octet_length(\"BundleHash\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_deps_hash", "octet_length(\"DependencyFingerprint\") = 32");
            table.HasCheckConstraint("ck_policy_analysis_runs_findings", "length(btrim(\"FindingsJson\")) >= 2");
            table.HasCheckConstraint("ck_policy_analysis_runs_tests", "length(btrim(\"TestResultsJson\")) >= 2");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.RevisionId).IsRequired();
        builder.Property(e => e.RevisionContentHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.LogicalEffectiveHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.AnalysisContextHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.EvidenceContextHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.TopologyProjectionHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.ImpactSetHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.PerDeviceAnalysisHashes).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.BundleHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.DependencyFingerprint).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.RiskLevel).HasColumnType("text").IsRequired();
        builder.Property(e => e.EvidenceSignalsPresent).IsRequired();
        builder.Property(e => e.AnalyzerVersion).HasColumnType("text").IsRequired();
        builder.Property(e => e.PolicySchemaVersion).HasColumnType("text").IsRequired();
        builder.Property(e => e.PipelineVersion).HasColumnType("text").IsRequired();
        builder.Property(e => e.FindingsJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.TestResultsJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => e.RevisionId).HasDatabaseName("ix_policy_analysis_runs_revision");
        builder.HasOne<PolicyRevisionEntity>()
            .WithMany()
            .HasForeignKey(e => e.RevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
