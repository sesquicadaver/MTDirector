using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Endpoint;

/// <summary>EF Core store for endpoint response assessments (M7.2-03).</summary>
public sealed class EfResponseAssessmentStore : IResponseAssessmentStore
{
    private readonly MfcDbContext _db;

    public EfResponseAssessmentStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<ResponseAssessment?> GetActiveByEndpointAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default)
    {
        ResponseAssessmentEntity? entity = await _db.ResponseAssessments.AsNoTracking()
            .Where(e => e.EndpointId == endpointId.Value && e.Status == (int)ResponseAssessmentStatus.Active)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task SaveAsync(ResponseAssessment assessment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ResponseAssessmentEntity entity = ToEntity(assessment);
        ResponseAssessmentEntity? existing = await _db.ResponseAssessments
            .SingleOrDefaultAsync(e => e.AssessmentId == entity.AssessmentId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            _db.ResponseAssessments.Add(entity);
        }
        else
        {
            existing.IncidentId = entity.IncidentId;
            existing.EndpointId = entity.EndpointId;
            existing.PresenceId = entity.PresenceId;
            existing.EnforcementNodeId = entity.EnforcementNodeId;
            existing.Feasibility = entity.Feasibility;
            existing.Status = entity.Status;
            existing.CreatedAt = entity.CreatedAt;
            existing.InvalidatedAt = entity.InvalidatedAt;
            existing.InvalidationReason = entity.InvalidationReason;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ResponseAssessment ToDomain(ResponseAssessmentEntity entity)
        => ResponseAssessment.Reconstitute(
            new AssessmentId(entity.AssessmentId),
            new IncidentId(entity.IncidentId),
            new EndpointId(entity.EndpointId),
            new PresenceId(entity.PresenceId),
            new NodeId(entity.EnforcementNodeId),
            (ResponseAssessmentFeasibility)entity.Feasibility,
            (ResponseAssessmentStatus)entity.Status,
            entity.CreatedAt,
            entity.InvalidatedAt,
            entity.InvalidationReason);

    private static ResponseAssessmentEntity ToEntity(ResponseAssessment assessment)
        => new()
        {
            AssessmentId = assessment.AssessmentId.Value,
            IncidentId = assessment.IncidentId.Value,
            EndpointId = assessment.EndpointId.Value,
            PresenceId = assessment.PresenceId.Value,
            EnforcementNodeId = assessment.EnforcementNodeId.Value,
            Feasibility = (int)assessment.Feasibility,
            Status = (int)assessment.Status,
            CreatedAt = assessment.CreatedAt,
            InvalidatedAt = assessment.InvalidatedAt,
            InvalidationReason = assessment.InvalidationReason,
        };
}
