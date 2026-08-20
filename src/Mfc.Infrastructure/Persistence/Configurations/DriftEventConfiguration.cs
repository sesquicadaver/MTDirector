using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class DriftEventConfiguration : IEntityTypeConfiguration<DriftEventEntity>
{
    public void Configure(EntityTypeBuilder<DriftEventEntity> builder)
    {
        builder.ToTable("drift_events", table =>
        {
            table.HasCheckConstraint(
                "ck_drift_events_baseline",
                "\"BaselineCommittedHash\" IS NULL OR octet_length(\"BaselineCommittedHash\") = 32");
            table.HasCheckConstraint(
                "ck_drift_events_actual",
                "\"ActualManagedResourceHash\" IS NULL OR octet_length(\"ActualManagedResourceHash\") = 32");
            table.HasCheckConstraint(
                "ck_drift_events_desired",
                "\"DesiredArtifactHashIgnoredForBaseline\" IS NULL OR octet_length(\"DesiredArtifactHashIgnoredForBaseline\") = 32");
            table.HasCheckConstraint(
                "ck_drift_events_semantic_hash",
                "\"SemanticDiffHash\" IS NULL OR octet_length(\"SemanticDiffHash\") = 32");
            table.HasCheckConstraint("ck_drift_events_outcome", "\"Outcome\" BETWEEN 0 AND 4");
            table.HasCheckConstraint("ck_drift_events_immutable", "\"Immutable\" = TRUE");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.DeviceId).IsRequired();
        builder.Property(e => e.NodeId).IsRequired();
        builder.Property(e => e.BaselineCommittedHash).HasColumnType("bytea");
        builder.Property(e => e.ActualManagedResourceHash).HasColumnType("bytea");
        builder.Property(e => e.DesiredArtifactHashIgnoredForBaseline).HasColumnType("bytea");
        builder.Property(e => e.Outcome).IsRequired();
        builder.Property(e => e.ConfigurationDriftPresent).IsRequired();
        builder.Property(e => e.BlocksDeployment).IsRequired();
        builder.Property(e => e.FindingsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.SemanticDiffCanonical).HasColumnType("text");
        builder.Property(e => e.SemanticDiffHash).HasColumnType("bytea");
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.Immutable).IsRequired().HasDefaultValue(true);
        builder.HasIndex(e => e.DeviceId);
        builder.HasIndex(e => e.NodeId);
        builder.HasIndex(e => new { e.DeviceId, e.CreatedAtUtc });
        builder.HasOne<DeviceEntity>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NodeEntity>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
