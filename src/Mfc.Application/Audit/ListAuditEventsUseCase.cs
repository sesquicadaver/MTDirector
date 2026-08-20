using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Audit;

/// <summary>Bounded newest-first audit list query (M6-04).</summary>
public sealed class ListAuditEventsQuery
{
    public required string Actor { get; init; }

    /// <summary>Requested page size; server clamps to [1, <see cref="ListAuditEventsUseCase.MaxPageSize"/>].</summary>
    public int PageSize { get; init; } = ListAuditEventsUseCase.DefaultPageSize;
}

/// <summary>Lists immutable audit events newest-first (audit.read). No mutate surface.</summary>
public sealed class ListAuditEventsUseCase
{
    public const int DefaultPageSize = 100;

    public const int MaxPageSize = 200;

    private readonly IAuthorizationBoundary _auth;
    private readonly IAuditEventReadStore _store;

    public ListAuditEventsUseCase(IAuthorizationBoundary auth, IAuditEventReadStore store)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(store);
        _auth = auth;
        _store = store;
    }

    public async Task<ApplicationResult<IReadOnlyList<AuditEventView>>> ExecuteAsync(
        ListAuditEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.AuditRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        int limit = query.PageSize <= 0 ? DefaultPageSize : Math.Clamp(query.PageSize, 1, MaxPageSize);
        IReadOnlyList<AuditEventRecord> rows = await _store
            .ListNewestAsync(limit, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok<IReadOnlyList<AuditEventView>>(
            rows.Select(static r => new AuditEventView
            {
                Id = r.Id,
                OccurredAtUtc = r.OccurredAtUtc,
                Actor = r.Actor,
                Action = r.Action,
                PayloadJson = r.PayloadJson,
            }).ToArray());
    }
}
