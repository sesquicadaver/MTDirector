using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Ordered set of chain contracts owned exclusively by COMPANY_BASELINE (Policy Model §15).
/// Site/Node overlays cannot define or mutate contracts.
/// </summary>
public sealed class ChainContractSet
{
    private readonly ChainContract[] _contracts;

    private ChainContractSet(ChainContract[] contracts) => _contracts = contracts;

    public IReadOnlyList<ChainContract> Items => _contracts;

    public int Count => _contracts.Length;

    /// <summary>Empty set for drafts that have not yet defined enabled surfaces.</summary>
    public static ChainContractSet Empty { get; } = new([]);

    public static ChainContractSet CreateForCompanyBaseline(
        IEnumerable<ChainContract> contracts,
        PolicyRuntimeMode runtimeMode)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        List<ChainContract> list = contracts.ToList();
        HashSet<(IpAddressFamily Family, PolicyFilterChain Chain)> seen = [];
        foreach (ChainContract contract in list)
        {
            ArgumentNullException.ThrowIfNull(contract);
            if (contract.DefaultDisposition == ChainDefaultDisposition.ReturnToUnmanaged
                && runtimeMode != PolicyRuntimeMode.MigrationCoexistence)
            {
                throw new DomainInvariantException(
                    "RETURN_TO_UNMANAGED is allowed only in migration/coexistence mode.");
            }

            if (!seen.Add((contract.Family, contract.Chain)))
            {
                throw new DomainInvariantException(
                    $"Duplicate chain contract for {PolicyPipelineV1.FormatFamily(contract.Family)}/" +
                    $"{PolicyPipelineV1.FormatFilterChain(contract.Chain)}.");
            }
        }

        ChainContract[] ordered = list
            .OrderBy(static c => c.Family)
            .ThenBy(static c => c.Chain)
            .ToArray();
        return new ChainContractSet(ordered);
    }

    /// <summary>
    /// Overlays and exceptions must not carry chain contracts (Policy Model §15 rules 2–3).
    /// </summary>
    public static ChainContractSet ForNonBaseline(PolicyKind kind)
    {
        if (kind == PolicyKind.CompanyBaseline)
        {
            throw new DomainInvariantException(
                "COMPANY_BASELINE must use CreateForCompanyBaseline instead of ForNonBaseline.");
        }

        return Empty;
    }

    public ChainContract? Find(IpAddressFamily family, PolicyFilterChain chain)
        => _contracts.FirstOrDefault(c => c.Family == family && c.Chain == chain);

    public void EnsureCannotBeChangedBy(PolicyKind kind)
    {
        if (kind is PolicyKind.SiteOverlay or PolicyKind.NodeOverlay or PolicyKind.Exception or PolicyKind.IncidentDenyOverlay)
        {
            if (_contracts.Length > 0)
            {
                throw new DomainInvariantException(
                    $"{PolicyCanonicalWriter.FormatKind(kind)} cannot define or change chain contracts; " +
                    "only COMPANY_BASELINE may set them.");
            }

            return;
        }

        if (kind != PolicyKind.CompanyBaseline)
        {
            throw new DomainInvariantException($"Unknown policy kind '{kind}'.");
        }
    }
}
