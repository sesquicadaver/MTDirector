using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Inventory;

/// <summary>EF Core site aggregate store.</summary>
public sealed class EfSiteStore : ISiteStore
{
    private readonly MfcDbContext _db;

    public EfSiteStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<bool> CodeExistsAsync(SiteCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _db.Sites.AsNoTracking().AnyAsync(s => s.Code == code.Value, cancellationToken);
    }

    public async Task AddAsync(Site site, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(site);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.Sites.Add(new SiteEntity
        {
            Id = site.Id.Value,
            Code = site.Code.Value,
            Name = site.Name.Value,
            Status = (short)site.Status,
            RowVersion = (long)site.RowVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Site?> GetAsync(SiteId id, CancellationToken cancellationToken = default)
    {
        SiteEntity? entity = await _db.Sites.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<SiteEntity> rows = await _db.Sites.AsNoTracking()
            .OrderBy(s => s.Code)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<SitePage> ListPageAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(limit, 1, 200);
        IQueryable<SiteEntity> query = _db.Sites.AsNoTracking()
            .OrderBy(s => s.Code)
            .ThenBy(s => s.Id);

        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out string code, out Guid id))
        {
            query = query.Where(s =>
                s.Code.CompareTo(code) > 0
                || (s.Code == code && s.Id.CompareTo(id) > 0));
        }

        List<SiteEntity> page = await query.Take(take + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        string? next = null;
        if (page.Count > take)
        {
            SiteEntity last = page[take - 1];
            next = EncodeCursor(last.Code, last.Id);
            page.RemoveAt(take);
        }

        return new SitePage
        {
            Items = page.Select(ToDomain).ToArray(),
            NextCursor = next,
        };
    }

    private static Site ToDomain(SiteEntity entity)
        => Site.Reconstitute(
            new SiteId(entity.Id),
            SiteCode.Create(entity.Code),
            NonEmptyName.Create(entity.Name),
            (SiteStatus)entity.Status,
            (ulong)entity.RowVersion);

    private static string EncodeCursor(string code, Guid id)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{code}\n{id:D}"));

    private static bool TryDecodeCursor(string cursor, out string code, out Guid id)
    {
        code = string.Empty;
        id = Guid.Empty;
        try
        {
            string decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            string[] parts = decoded.Split('\n');
            if (parts.Length != 2 || !Guid.TryParse(parts[1], out id))
            {
                return false;
            }

            code = parts[0];
            return code.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
