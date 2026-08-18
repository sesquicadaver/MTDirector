using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>Permanent anchor key family+chain (Onboarding Spec §18–§19).</summary>
public sealed class AnchorKey : IEquatable<AnchorKey>
{
    public AnchorKey(IpAddressFamily family, FilterBuiltInContext chain)
    {
        if (chain is not (FilterBuiltInContext.Input or FilterBuiltInContext.Forward or FilterBuiltInContext.Output))
        {
            throw new DomainInvariantException($"Unsupported anchor chain '{chain}'.");
        }

        Family = family;
        Chain = chain;
    }

    public static AnchorKey Create(IpAddressFamily family, FilterBuiltInContext chain)
        => new(family, chain);

    public IpAddressFamily Family { get; }

    public FilterBuiltInContext Chain { get; }

    /// <summary>Permanent marker comment, e.g. <c>mfc:anchor:v1:4:i</c>.</summary>
    public string Marker => $"mfc:anchor:v1:{(Family == IpAddressFamily.IPv4 ? '4' : '6')}:{ChainCode(Chain)}";

    public static string ChainCode(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "i",
            FilterBuiltInContext.Forward => "f",
            FilterBuiltInContext.Output => "o",
            _ => throw new DomainInvariantException($"Unsupported anchor chain '{chain}'."),
        };

    public bool Equals(AnchorKey? other)
        => other is not null && Family == other.Family && Chain == other.Chain;

    public override bool Equals(object? obj) => obj is AnchorKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Family, Chain);

    public override string ToString() => Marker;
}
