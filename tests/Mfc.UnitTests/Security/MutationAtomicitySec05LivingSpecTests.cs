using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Inventory;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-05 (#378) — atomic mutation + idempotency + audit boundary.</summary>
public sealed class MutationAtomicitySec05LivingSpecTests
{
    [Fact]
    public async Task Ac1CreateSiteRunsMutationIdempotencyAndAuditInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        SpyUnitOfWork unitOfWork = new();
        Guid key = Guid.NewGuid();

        CreateSiteUseCase useCase = new(auth, sites, idempotency, audit, unitOfWork);
        ApplicationResult<SiteView> result = await useCase.ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = key,
                Code = "SEC05",
                Name = "Sec05 Site",
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.Single(await sites.ListAsync());
        Assert.Contains(audit.Events, e => e.Action == CreateSiteUseCase.Operation);

        // Replay path finds the same resource for the same idempotency key.
        ApplicationResult<SiteView> replay = await useCase.ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = key,
                Code = "SEC05",
                Name = "Sec05 Site",
            });
        Assert.True(replay.IsSuccess);
        Assert.Equal(result.Value!.Id, replay.Value!.Id);
        Assert.Equal(1, unitOfWork.ExecuteCount);
    }

    [Fact]
    public async Task Ac2FailureAfterSiteAddRollsBackWhenUnitOfWorkCompensates()
    {
        FakeAuthorizationBoundary auth = new();
        ClearableSiteStore sites = new();
        BoomIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        CompensatingUnitOfWork unitOfWork = new(sites, audit);

        CreateSiteUseCase useCase = new(auth, sites, idempotency, audit, unitOfWork);
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                Code = "BOOM1",
                Name = "Boom",
            }));

        Assert.Empty(await sites.ListAsync());
        Assert.Empty(audit.Events);
        Assert.Equal(1, unitOfWork.ExecuteCount);
    }

    [Fact]
    public void Ac3AuditWriterJoinsAmbientTransactionInsteadOfNesting()
    {
        string path = Path.Combine(FindRepoRoot(), "src", "Mfc.Infrastructure", "Audit", "EfAuditEventWriter.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("CurrentTransaction", source, StringComparison.Ordinal);
        Assert.Contains("ownsTransaction", source, StringComparison.Ordinal);
        Assert.Contains("SEC-05", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4InventoryCreateSiteSourceUsesUnitOfWorkBoundary()
    {
        string path = Path.Combine(FindRepoRoot(), "src", "Mfc.Application", "Inventory", "InventoryUseCases.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("_unitOfWork.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return action(cancellationToken);
        }
    }

    private sealed class CompensatingUnitOfWork(ClearableSiteStore sites, FakeAuditEventWriter audit) : IUnitOfWork
    {
        public int ExecuteCount { get; private set; }

        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                sites.Clear();
                audit.Events.Clear();
                throw;
            }
        }
    }

    private sealed class BoomIdempotencyStore : IIdempotencyStore
    {
        public Task<IdempotencyLookupResult> TryGetAsync(
            string actor,
            string operation,
            Guid idempotencyKey,
            ReadOnlyMemory<byte> requestHash,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new IdempotencyLookupResult { Found = false });

        public Task SaveAsync(
            string actor,
            string operation,
            Guid idempotencyKey,
            ReadOnlyMemory<byte> requestHash,
            Guid resourceId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("idempotency boom");
    }

    private sealed class ClearableSiteStore : ISiteStore
    {
        private readonly Dictionary<Guid, Site> _byId = [];
        private readonly HashSet<string> _codes = new(StringComparer.Ordinal);

        public void Clear()
        {
            _byId.Clear();
            _codes.Clear();
        }

        public Task<bool> CodeExistsAsync(SiteCode code, CancellationToken cancellationToken = default)
            => Task.FromResult(_codes.Contains(code.Value));

        public Task AddAsync(Site site, CancellationToken cancellationToken = default)
        {
            _byId[site.Id.Value] = site;
            _codes.Add(site.Code.Value);
            return Task.CompletedTask;
        }

        public Task<Site?> GetAsync(SiteId id, CancellationToken cancellationToken = default)
            => Task.FromResult(_byId.TryGetValue(id.Value, out Site? site) ? site : null);

        public Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Site>>(_byId.Values.ToArray());

        public Task<SitePage> ListPageAsync(int limit, string? cursor, CancellationToken cancellationToken = default)
            => Task.FromResult(new SitePage { Items = _byId.Values.ToArray(), NextCursor = null });
    }
}
