using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingAnchorPlacementConfiguration : IEntityTypeConfiguration<OnboardingAnchorPlacementEntity>
{
    public void Configure(EntityTypeBuilder<OnboardingAnchorPlacementEntity> builder)
    {
        builder.ToTable("onboarding_anchor_placements", table =>
        {
            table.HasCheckConstraint("ck_onboarding_anchor_placements_family", "\"Family\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("ck_onboarding_anchor_placements_chain", "\"Chain\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_onboarding_anchor_placements_mode", "\"Mode\" BETWEEN 0 AND 1");
            table.HasCheckConstraint(
                "ck_onboarding_anchor_placements_ref_hash",
                "\"ReferenceRuleFingerprint\" IS NULL OR octet_length(\"ReferenceRuleFingerprint\") = 32");
            table.HasCheckConstraint(
                "ck_onboarding_anchor_placements_pred_hash",
                "\"ExpectedPredecessorFingerprint\" IS NULL OR octet_length(\"ExpectedPredecessorFingerprint\") = 32");
            table.HasCheckConstraint(
                "ck_onboarding_anchor_placements_succ_hash",
                "\"ExpectedSuccessorFingerprint\" IS NULL OR octet_length(\"ExpectedSuccessorFingerprint\") = 32");
            table.HasCheckConstraint("ck_onboarding_anchor_placements_ordinal", "\"ExpectedAnchorOrdinal\" >= 0");
            table.HasCheckConstraint(
                "ck_onboarding_anchor_placements_before_ref",
                "(\"Mode\" = 0 AND \"ReferenceRuleFingerprint\" IS NOT NULL AND \"ReferenceOccurrenceRank\" IS NOT NULL) OR (\"Mode\" = 1 AND \"ReferenceRuleFingerprint\" IS NULL AND \"ReferenceOccurrenceRank\" IS NULL)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.DevicePlanId).IsRequired();
        builder.Property(e => e.Family).IsRequired();
        builder.Property(e => e.Chain).IsRequired();
        builder.Property(e => e.Mode).IsRequired();
        builder.Property(e => e.ReferenceRuleFingerprint).HasColumnType("bytea");
        builder.Property(e => e.ExpectedPredecessorFingerprint).HasColumnType("bytea");
        builder.Property(e => e.ExpectedSuccessorFingerprint).HasColumnType("bytea");
        builder.Property(e => e.ExpectedAnchorOrdinal).IsRequired();
        builder.HasIndex(e => new { e.DevicePlanId, e.Family, e.Chain })
            .IsUnique()
            .HasDatabaseName("uq_onboarding_anchor_placements_plan_key");
    }
}
