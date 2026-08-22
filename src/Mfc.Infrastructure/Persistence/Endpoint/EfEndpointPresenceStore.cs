using System.Text.Json;
using System.Text.Json.Serialization;
using Mfc.Domain;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Endpoint;

/// <summary>EF Core store for endpoint presence intervals and routing contexts (M7.2-02).</summary>
public sealed class EfEndpointPresenceStore : IEndpointPresenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly MfcDbContext _db;

    public EfEndpointPresenceStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<EndpointPresenceInterval?> GetActiveIntervalAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default)
    {
        EndpointPresenceIntervalEntity? entity = await _db.EndpointPresenceIntervals.AsNoTracking()
            .Where(e => e.EndpointId == endpointId.Value && e.ValidUntil == null)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToInterval(entity);
    }

    public async Task<EndpointPresenceInterval?> GetIntervalAsOfAsync(
        EndpointId endpointId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset asOf = asOfUtc.ToUniversalTime();
        List<EndpointPresenceIntervalEntity> entities = await _db.EndpointPresenceIntervals.AsNoTracking()
            .Where(e => e.EndpointId == endpointId.Value)
            .OrderByDescending(e => e.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        EndpointPresenceIntervalEntity? match = entities
            .FirstOrDefault(e => e.ValidFrom <= asOf && (e.ValidUntil == null || e.ValidUntil > asOf));
        return match is null ? null : ToInterval(match);
    }

    public async Task<EndpointRoutingContext?> GetRoutingContextAsync(
        PresenceId presenceId,
        CancellationToken cancellationToken = default)
    {
        EndpointRoutingContextEntity? entity = await _db.EndpointRoutingContexts.AsNoTracking()
            .SingleOrDefaultAsync(e => e.PresenceId == presenceId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToRoutingContext(entity);
    }

    public async Task<EndpointRoutingContext?> GetRoutingContextAsOfAsync(
        EndpointId endpointId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        EndpointPresenceInterval? interval = await GetIntervalAsOfAsync(endpointId, asOfUtc, cancellationToken)
            .ConfigureAwait(false);
        return interval is null
            ? null
            : await GetRoutingContextAsync(interval.PresenceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveMigrationAsync(
        EndpointPresenceInterval? closedInterval,
        EndpointPresenceInterval openedInterval,
        EndpointRoutingContext routingContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openedInterval);
        ArgumentNullException.ThrowIfNull(routingContext);

        if (closedInterval is not null)
        {
            EndpointPresenceIntervalEntity? tracked = await _db.EndpointPresenceIntervals
                .SingleOrDefaultAsync(e => e.PresenceId == closedInterval.PresenceId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (tracked is null)
            {
                throw new InvalidOperationException(
                    $"Closed presence interval '{closedInterval.PresenceId}' was not found.");
            }

            tracked.ValidUntil = closedInterval.ValidUntil;
            EndpointRoutingContextEntity? trackedContext = await _db.EndpointRoutingContexts
                .SingleOrDefaultAsync(e => e.PresenceId == closedInterval.PresenceId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (trackedContext is not null)
            {
                trackedContext.ValidUntil = closedInterval.ValidUntil;
            }
        }

        bool activeExists = await _db.EndpointPresenceIntervals
            .AnyAsync(
                e => e.EndpointId == openedInterval.EndpointId.Value && e.ValidUntil == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (activeExists && closedInterval is null)
        {
            throw new DomainInvariantException(
                $"Endpoint '{openedInterval.EndpointId}' already has an active presence interval ({EndpointPresenceCodes.OverlappingActiveInterval}).");
        }

        _db.EndpointPresenceIntervals.Add(ToIntervalEntity(openedInterval));
        _db.EndpointRoutingContexts.Add(ToRoutingContextEntity(routingContext));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static EndpointPresenceIntervalEntity ToIntervalEntity(EndpointPresenceInterval interval) => new()
    {
        PresenceId = interval.PresenceId.Value,
        EndpointId = interval.EndpointId.Value,
        SiteId = interval.SiteId.Value,
        NodeId = interval.NodeId.Value,
        DeviceId = interval.DeviceId?.Value,
        VlanId = interval.VlanId,
        Vrf = interval.Vrf,
        SourceAddress = interval.SourceAddress,
        MacAddress = interval.MacAddress,
        AttributionCertainty = (int)interval.AttributionCertainty,
        ValidFrom = interval.ValidFrom,
        ValidUntil = interval.ValidUntil,
    };

    private static EndpointRoutingContextEntity ToRoutingContextEntity(EndpointRoutingContext context) => new()
    {
        PresenceId = context.PresenceId.Value,
        EndpointId = context.EndpointId.Value,
        SiteId = context.SiteId.Value,
        NodeId = context.NodeId.Value,
        VlanId = context.VlanId,
        Vrf = context.Vrf,
        SourceAddress = context.SourceAddress,
        CorporateRouteTraceJson = SerializeTrace(context.CorporateRouteTrace),
        InternetRouteTraceJson = SerializeTrace(context.InternetRouteTrace),
        WazuhRouteTraceJson = SerializeTrace(context.WazuhRouteTrace),
        ValidFrom = context.ValidFrom,
        ValidUntil = context.ValidUntil,
    };

    private static EndpointPresenceInterval ToInterval(EndpointPresenceIntervalEntity entity)
        => EndpointPresenceInterval.Reconstitute(
            new PresenceId(entity.PresenceId),
            new EndpointId(entity.EndpointId),
            new SiteId(entity.SiteId),
            new NodeId(entity.NodeId),
            entity.SourceAddress,
            (EndpointAttributionCertainty)entity.AttributionCertainty,
            entity.ValidFrom,
            entity.ValidUntil,
            entity.DeviceId is null ? null : new DeviceId(entity.DeviceId.Value),
            entity.VlanId,
            entity.Vrf,
            entity.MacAddress);

    private static EndpointRoutingContext ToRoutingContext(EndpointRoutingContextEntity entity)
        => EndpointRoutingContext.Reconstitute(
            new EndpointId(entity.EndpointId),
            new PresenceId(entity.PresenceId),
            new SiteId(entity.SiteId),
            new NodeId(entity.NodeId),
            entity.SourceAddress,
            entity.ValidFrom,
            entity.ValidUntil,
            entity.VlanId,
            entity.Vrf,
            DeserializeTrace(entity.CorporateRouteTraceJson),
            DeserializeTrace(entity.InternetRouteTraceJson),
            DeserializeTrace(entity.WazuhRouteTraceJson));

    private static string? SerializeTrace(RouteResolutionTrace? trace)
        => trace is null ? null : JsonSerializer.Serialize(trace, JsonOptions);

    private static RouteResolutionTrace? DeserializeTrace(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<RouteResolutionTrace>(json, JsonOptions);
}
