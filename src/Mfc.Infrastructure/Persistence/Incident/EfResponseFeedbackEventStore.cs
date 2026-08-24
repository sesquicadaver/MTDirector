using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Incident.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Incident;

/// <summary>EF Core append-only store for <see cref="ResponseFeedbackEvent"/> (M7.4-05).</summary>
public sealed class EfResponseFeedbackEventStore : IResponseFeedbackEventStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly MfcDbContext _db;

    public EfResponseFeedbackEventStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AppendAsync(ResponseFeedbackEvent feedbackEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedbackEvent);
        _db.ResponseFeedbackEvents.Add(ToEntity(feedbackEvent));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResponseFeedbackEvent?> GetAsync(
        ResponseFeedbackEventId id,
        CancellationToken cancellationToken = default)
    {
        ResponseFeedbackEventEntity? entity = await _db.ResponseFeedbackEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<ResponseFeedbackEvent>> ListByIncidentAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default)
    {
        List<ResponseFeedbackEventEntity> rows = await _db.ResponseFeedbackEvents.AsNoTracking()
            .Where(e => e.IncidentId == incidentId.Value)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<ResponseFeedbackEvent>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        List<ResponseFeedbackEventEntity> rows = await _db.ResponseFeedbackEvents.AsNoTracking()
            .Where(e => e.NodeId == nodeId.Value)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static ResponseFeedbackEventEntity ToEntity(ResponseFeedbackEvent feedbackEvent) => new()
    {
        Id = feedbackEvent.Id.Value,
        Kind = (short)feedbackEvent.Kind,
        EventCode = feedbackEvent.EventCode,
        IncidentId = feedbackEvent.IncidentId.Value,
        NodeId = feedbackEvent.NodeId.Value,
        DeviceIdsJson = JsonSerializer.Serialize(
            feedbackEvent.DeviceIds.Select(static d => d.Value).ToArray(),
            JsonOptions),
        PolicyHash = ToBytes(feedbackEvent.PolicyHash),
        ArtifactHash = ToBytes(feedbackEvent.ArtifactHash),
        PlanHash = ToBytes(feedbackEvent.PlanHash),
        VerificationResults = feedbackEvent.VerificationResults,
        RollbackStatus = feedbackEvent.RollbackStatus,
        ResidualRisk = feedbackEvent.ResidualRisk,
        CorrelationId = feedbackEvent.CorrelationId,
        CreatedAtUtc = feedbackEvent.CreatedAtUtc,
        Immutable = true,
    };

    private static ResponseFeedbackEvent ToDomain(ResponseFeedbackEventEntity entity)
    {
        Guid[] deviceIds = JsonSerializer.Deserialize<Guid[]>(entity.DeviceIdsJson, JsonOptions) ?? [];
        return ResponseFeedbackEvent.Reconstitute(
            new ResponseFeedbackEventId(entity.Id),
            (ResponseFeedbackEventKind)entity.Kind,
            entity.EventCode,
            new IncidentId(entity.IncidentId),
            new NodeId(entity.NodeId),
            deviceIds.Select(static id => new DeviceId(id)).ToArray(),
            FromBytes(entity.PolicyHash),
            FromBytes(entity.ArtifactHash),
            FromBytes(entity.PlanHash),
            entity.VerificationResults,
            entity.RollbackStatus,
            entity.ResidualRisk,
            entity.CorrelationId,
            entity.CreatedAtUtc);
    }

    private static byte[]? ToBytes(Hash256? hash)
        => hash is null ? null : hash.Bytes.ToArray();

    private static Hash256? FromBytes(byte[]? bytes)
        => bytes is null || bytes.Length == 0 ? null : Hash256.Create(bytes);
}
