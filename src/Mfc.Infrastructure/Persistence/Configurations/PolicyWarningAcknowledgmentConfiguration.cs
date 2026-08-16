using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class PolicyWarningAcknowledgmentConfiguration
    : IEntityTypeConfiguration<PolicyWarningAcknowledgmentEntity>
{
    public void Configure(EntityTypeBuilder<PolicyWarningAcknowledgmentEntity> builder)
    {
        builder.ToTable("warning_acknowledgments", table =>
        {
            table.HasCheckConstraint(
                "ck_warning_acknowledgments_hash",
                "octet_length(\"WarningHash\") = 32");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.AnalysisRunId).IsRequired();
        builder.Property(e => e.WarningHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.AcknowledgedBy).IsRequired();
        builder.Property(e => e.AcknowledgedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.AnalysisRunId, e.WarningHash, e.AcknowledgedBy })
            .IsUnique()
            .HasDatabaseName("uq_warning_acknowledgments_run_hash_actor");
        builder.HasOne<PolicyAnalysisRunEntity>()
            .WithMany()
            .HasForeignKey(e => e.AnalysisRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
