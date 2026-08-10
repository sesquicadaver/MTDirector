using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Company-baseline chain contract for one family/chain surface (Policy Model §15).
/// ACCEPT as default disposition is impossible.
/// </summary>
public sealed class ChainContract : IEquatable<ChainContract>
{
    public IpAddressFamily Family { get; }

    public PolicyFilterChain Chain { get; }

    public ChainDefaultDisposition DefaultDisposition { get; }

    public RejectMode? RejectModeValue { get; }

    /// <summary>RETURN_TO_UNMANAGED always carries CRITICAL risk (Policy Model §15 rule 4).</summary>
    public bool IsCriticalRisk => DefaultDisposition == ChainDefaultDisposition.ReturnToUnmanaged;

    private ChainContract(
        IpAddressFamily family,
        PolicyFilterChain chain,
        ChainDefaultDisposition defaultDisposition,
        RejectMode? rejectMode)
    {
        Family = family;
        Chain = chain;
        DefaultDisposition = defaultDisposition;
        RejectModeValue = rejectMode;
    }

    public static ChainContract Create(
        IpAddressFamily family,
        PolicyFilterChain chain,
        ChainDefaultDisposition defaultDisposition,
        RejectMode? rejectMode,
        PolicyRuntimeMode runtimeMode)
    {
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported chain-contract family '{family}'.");
        }

        if (chain is not (PolicyFilterChain.Input or PolicyFilterChain.Forward or PolicyFilterChain.Output))
        {
            throw new DomainInvariantException($"Unsupported chain-contract chain '{chain}'.");
        }

        switch (defaultDisposition)
        {
            case ChainDefaultDisposition.Drop:
                if (rejectMode is not null)
                {
                    throw new DomainInvariantException("DROP default disposition must not set reject_mode.");
                }

                break;

            case ChainDefaultDisposition.Reject:
                if (rejectMode is null)
                {
                    throw new DomainInvariantException("REJECT default disposition requires reject_mode.");
                }

                break;

            case ChainDefaultDisposition.ReturnToUnmanaged:
                if (rejectMode is not null)
                {
                    throw new DomainInvariantException("RETURN_TO_UNMANAGED must not set reject_mode.");
                }

                if (runtimeMode != PolicyRuntimeMode.MigrationCoexistence)
                {
                    throw new DomainInvariantException(
                        "RETURN_TO_UNMANAGED is allowed only in migration/coexistence mode.");
                }

                break;

            default:
                throw new DomainInvariantException(
                    "ACCEPT as chain default disposition is forbidden; use DROP, REJECT, or RETURN_TO_UNMANAGED.");
        }

        return new ChainContract(family, chain, defaultDisposition, rejectMode);
    }

    public bool Equals(ChainContract? other)
        => other is not null
           && Family == other.Family
           && Chain == other.Chain
           && DefaultDisposition == other.DefaultDisposition
           && RejectModeValue == other.RejectModeValue;

    public override bool Equals(object? obj) => obj is ChainContract other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Family, Chain, DefaultDisposition, RejectModeValue);
}
