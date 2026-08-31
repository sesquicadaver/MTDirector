using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Policy;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

/// <summary>Optional kind filter; null lists every active kind.</summary>
public sealed class ListPoliciesQuery
{
    public required string Actor { get; init; }

    /// <summary>When set, only that kind; otherwise all active kinds.</summary>
    public PolicyKind? Kind { get; init; }
}

/// <summary>Lists active policy containers with the latest revision identity (W5-01 catalog browse).</summary>
public sealed class ListPoliciesUseCase
{
    private static readonly PolicyKind[] AllKinds =
    [
        PolicyKind.CompanyBaseline,
        PolicyKind.SiteOverlay,
        PolicyKind.NodeOverlay,
        PolicyKind.Exception,
        PolicyKind.IncidentDenyOverlay,
    ];

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;

    public ListPoliciesUseCase(IAuthorizationBoundary auth, IPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        _auth = auth;
        _policies = policies;
    }

    public async Task<ApplicationResult<PolicyCatalogListView>> ExecuteAsync(
        ListPoliciesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        PolicyKind[] kinds = query.Kind is PolicyKind filter
            ? [filter]
            : AllKinds;

        List<Policy> collected = [];
        foreach (PolicyKind kind in kinds)
        {
            IReadOnlyList<Policy> batch = await _policies
                .ListActiveByKindAsync(kind, cancellationToken)
                .ConfigureAwait(false);
            collected.AddRange(batch);
        }

        collected.Sort(static (left, right) =>
        {
            int byName = string.Compare(left.Name.Value, right.Name.Value, StringComparison.Ordinal);
            return byName != 0 ? byName : left.Id.Value.CompareTo(right.Id.Value);
        });

        List<PolicyCatalogItemView> items = [];
        foreach (Policy policy in collected)
        {
            IReadOnlyList<PolicyRevision> revisions = await _policies
                .ListRevisionsAsync(policy.Id, cancellationToken)
                .ConfigureAwait(false);
            PolicyRevision? latest = revisions.Count == 0 ? null : revisions[^1];
            if (latest is null)
            {
                continue;
            }

            items.Add(new PolicyCatalogItemView
            {
                PolicyId = policy.Id.Value,
                Name = policy.Name.Value,
                Kind = policy.Kind,
                OwnerScope = policy.OwnerScope,
                OwnerId = policy.OwnerId,
                LatestRevisionId = latest.Id.Value,
                LatestRevisionNumber = latest.RevisionNumber,
                LatestRevisionState = latest.State,
                ContentHashHex = latest.ContentHash.ToString(),
            });
        }

        return ApplicationResults.Ok(new PolicyCatalogListView { Policies = items });
    }
}
