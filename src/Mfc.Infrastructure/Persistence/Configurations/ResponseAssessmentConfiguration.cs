using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class ResponseAssessmentConfiguration : IEntityTypeConfiguration<ResponseAssessmentEntity>
{
    public void Configure(EntityTypeBuilder<ResponseAssessmentEntity> builder)
    {
        builder.ToTable("response_assessments");
        builder.HasKey(e => e.AssessmentId);
        builder.Property(e => e.AssessmentId).ValueGeneratedNever();
        builder.Property(e => e.IncidentId).IsRequired();
        builder.Property(e => e.EndpointId).IsRequired();
        builder.Property(e => e.PresenceId).IsRequired();
        builder.Property(e => e.EnforcementNodeId).IsRequired();
        builder.Property(e => e.Feasibility).IsRequired();
        builder.Property(e => e.VisibilityStatus).IsRequired();
        builder.Property(e => e.Confidence).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.InvalidatedAt);
        builder.Property(e => e.InvalidationReason).HasMaxLength(256);
        builder.HasIndex(e => e.EndpointId);
        builder.HasIndex(e => e.EndpointId)
            .IsUnique()
            .HasFilter("\"Status\" = 1")
            .HasDatabaseName("ux_response_assessments_active_endpoint");
    }
}
