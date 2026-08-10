using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Document-centric policy / revision persistence (Policy Model §66, M2-01).</summary>
public interface IPolicyStore
{
    Task AddPolicyAsync(Policy policy, CancellationToken cancellationToken = default);

    Task<Policy?> GetPolicyAsync(PolicyId id, CancellationToken cancellationToken = default);

    Task UpdatePolicyAsync(Policy policy, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new revision with Brotli-compressed payload; hash is of uncompressed bytes.</summary>
    Task AddRevisionAsync(PolicyRevision revision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists draft payload replacement and/or lifecycle state transitions.
    /// Approved payload bytes are never rewritten through this store.
    /// </summary>
    Task SaveRevisionAsync(PolicyRevision revision, CancellationToken cancellationToken = default);

    Task<PolicyRevision?> GetRevisionAsync(PolicyRevisionId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyRevision>> ListRevisionsAsync(
        PolicyId policyId,
        CancellationToken cancellationToken = default);

    /// <summary>Highest revision_number for the policy, or 0 when none exist.</summary>
    Task<uint> GetLatestRevisionNumberAsync(PolicyId policyId, CancellationToken cancellationToken = default);
}
