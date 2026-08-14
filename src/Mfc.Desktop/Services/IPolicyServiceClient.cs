using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only PolicyService client (ADR 0005 / M2-06 thin read).</summary>
public interface IPolicyServiceClient
{
    Task<ListRulesResponse> ListRulesAsync(
        Guid revisionId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> GetPolicyRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);
}
