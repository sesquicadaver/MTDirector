using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mfc.Infrastructure.Persistence.Configurations;

internal sealed class ResponseFeedbackEventConfiguration : IEntityTypeConfiguration<ResponseFeedbackEventEntity>
{
    public void Configure(EntityTypeBuilder<ResponseFeedbackEventEntity> builder)
    {
        builder.ToTable("response_feedback_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DeviceIdsJson).IsRequired();
        builder.Property(e => e.VerificationResults).HasMaxLength(4096);
        builder.Property(e => e.RollbackStatus).HasMaxLength(256);
        builder.Property(e => e.ResidualRisk).HasMaxLength(1024);
        builder.HasIndex(e => e.IncidentId);
        builder.HasIndex(e => e.NodeId);
        builder.HasIndex(e => new { e.IncidentId, e.CreatedAtUtc });
    }
}
