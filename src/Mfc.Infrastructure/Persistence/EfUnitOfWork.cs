using Mfc.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mfc.Infrastructure.Persistence;

/// <summary>EF Core unit of work over the shared scoped <see cref="MfcDbContext"/>.</summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly MfcDbContext _db;

    public EfUnitOfWork(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using IDbContextTransaction tx = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            throw;
        }
    }
}
