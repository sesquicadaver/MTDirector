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

    /// <summary>Parse permanent marker <c>mfc:anchor:v1:{4|6}:{i|f|o}</c>.</summary>
    public static bool TryParse(string? marker, out AnchorKey key)
    {
        key = null!;
        if (string.IsNullOrWhiteSpace(marker))
        {
            return false;
        }

        string[] parts = marker.Trim().Split(':', StringSplitOptions.None);
        if (parts.Length != 5
            || !string.Equals(parts[0], "mfc", StringComparison.Ordinal)
            || !string.Equals(parts[1], "anchor", StringComparison.Ordinal)
            || !string.Equals(parts[2], "v1", StringComparison.Ordinal))
        {
            return false;
        }

        IpAddressFamily family = parts[3] switch
        {
            "4" => IpAddressFamily.IPv4,
            "6" => IpAddressFamily.IPv6,
            _ => (IpAddressFamily)255,
        };
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            return false;
        }

        FilterBuiltInContext chain = parts[4] switch
        {
            "i" => FilterBuiltInContext.Input,
            "f" => FilterBuiltInContext.Forward,
            "o" => FilterBuiltInContext.Output,
            _ => (FilterBuiltInContext)255,
        };
        if (chain is not (FilterBuiltInContext.Input or FilterBuiltInContext.Forward or FilterBuiltInContext.Output))
        {
            return false;
        }

        key = new AnchorKey(family, chain);
        return true;
    }

    public bool Equals(AnchorKey? other)
        => other is not null && Family == other.Family && Chain == other.Chain;

    public override bool Equals(object? obj) => obj is AnchorKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Family, Chain);

    public override string ToString() => Marker;
}
