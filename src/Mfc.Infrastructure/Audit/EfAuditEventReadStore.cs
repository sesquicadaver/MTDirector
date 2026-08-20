using Mfc.Application.Abstractions.Audit;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Audit;

/// <summary>EF Core read-only listing of append-only audit events (M6-04).</summary>
public sealed class EfAuditEventReadStore : IAuditEventReadStore
{
    private readonly MfcDbContext _db;

    public EfAuditEventReadStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<AuditEventRecord>> ListNewestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be >= 1.");
        }

        List<AuditEventEntity> rows = await _db.AuditEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(static e => new AuditEventRecord
        {
            Id = e.Id,
            OccurredAtUtc = e.OccurredAtUtc,
            Actor = e.Actor,
            Action = e.Action,
            PayloadJson = e.PayloadJson,
        }).ToArray();
    }
}
