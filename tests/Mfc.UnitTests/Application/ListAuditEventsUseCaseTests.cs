using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Audit;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class ListAuditEventsUseCaseTests
{
    [Fact]
    public async Task ListsNewestFirstWithinBound()
    {
        FakeAuditEventReadStore store = new();
        DateTimeOffset t0 = DateTimeOffset.Parse("2026-08-20T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        store.Events.Add(new AuditEventRecord
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OccurredAtUtc = t0,
            Actor = "a",
            Action = "old",
            PayloadJson = "{}",
        });
        store.Events.Add(new AuditEventRecord
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OccurredAtUtc = t0.AddMinutes(1),
            Actor = "b",
            Action = "new",
            PayloadJson = "{}",
        });

        ListAuditEventsUseCase useCase = new(new FakeAuthorizationBoundary(), store);
        ApplicationResult<IReadOnlyList<AuditEventView>> result = await useCase.ExecuteAsync(
            new ListAuditEventsQuery { Actor = "tester", PageSize = 10 });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("new", result.Value[0].Action);
        Assert.Equal("old", result.Value[1].Action);
    }

    [Fact]
    public async Task ClampsPageSizeAndRequiresAuditRead()
    {
        FakeAuditEventReadStore store = new();
        for (int i = 0; i < 5; i++)
        {
            store.Events.Add(new AuditEventRecord
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-i),
                Actor = "a",
                Action = $"a{i}",
                PayloadJson = "{}",
            });
        }

        FakeAuthorizationBoundary auth = new();
        ListAuditEventsUseCase useCase = new(auth, store);
        ApplicationResult<IReadOnlyList<AuditEventView>> ok = await useCase.ExecuteAsync(
            new ListAuditEventsQuery { Actor = "tester", PageSize = 500 });
        Assert.True(ok.IsSuccess);
        Assert.Equal(5, ok.Value!.Count);
        Assert.Equal(ListAuditEventsUseCase.MaxPageSize, store.LastLimit);

        auth.DeniedPermissions.Add(ApplicationPermissions.AuditRead);
        ApplicationResult<IReadOnlyList<AuditEventView>> denied = await useCase.ExecuteAsync(
            new ListAuditEventsQuery { Actor = "tester", PageSize = 10 });
        Assert.True(denied.IsFailure);
    }

    private sealed class FakeAuditEventReadStore : IAuditEventReadStore
    {
        public List<AuditEventRecord> Events { get; } = [];

        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<AuditEventRecord>> ListNewestAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<AuditEventRecord>>(
                Events.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Take(limit).ToArray());
        }
    }
}
