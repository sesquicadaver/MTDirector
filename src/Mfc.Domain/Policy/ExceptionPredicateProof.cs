using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Fail-closed subset / overlap proofs for exception predicates (M2-08 L5 + M2-09 interval algebra).
/// tcp_flags and ipsec stay equality on the subset gate; addresses/ports/services/zones use packet space.
/// </summary>
public static class ExceptionPredicateProof
{
    /// <summary>Both source and destination address selectors unconstrained (L5).</summary>
    public static bool IsAddressUniverse(TrafficPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return IsUnconstrainedAddress(predicate.SourceAddresses)
               && IsUnconstrainedAddress(predicate.DestinationAddresses);
    }

    /// <summary>
    /// Returns a blocker code when <paramref name="exception"/> is not a fail-closed subset of
    /// <paramref name="target"/>; otherwise null.
    /// </summary>
    public static string? CheckSubset(
        TrafficPredicate exception,
        TrafficPredicate target,
        IpAddressFamily family,
        PolicyFilterChain chain,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);
        if (IsAddressUniverse(target))
        {
            return PolicyExceptionCodes.UniverseTarget;
        }

        string? flags = CheckTcpFlags(exception.TcpFlags, target.TcpFlags)
                        ?? CheckIpsec(exception.IpsecPolicy, target.IpsecPolicy);
        if (flags is not null)
        {
            return flags;
        }

        PredicateAlgebraResult inner = PredicateNormalizer.Normalize(exception, family, chain, addresses, services);
        if (inner.IsFailure)
        {
            return inner.Code;
        }

        PredicateAlgebraResult cover = PredicateNormalizer.Normalize(target, family, chain, addresses, services);
        if (cover.IsFailure)
        {
            return cover.Code;
        }

        return PredicateAlgebra.IsSubset(inner.Value!, cover.Value!)
            ? null
            : PolicyExceptionCodes.NotSubset;
    }

    /// <summary>
    /// Returns <see cref="PolicyExceptionCodes.Overlap"/> when the predicates share packet space,
    /// <see cref="PredicateAlgebraCodes.ComplexityLimit"/> or a selector code on proof failure,
    /// and null when they are disjoint.
    /// </summary>
    public static string? CheckOverlap(
        TrafficPredicate left,
        TrafficPredicate right,
        IpAddressFamily family,
        PolicyFilterChain chain,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);
        PredicateAlgebraResult a = PredicateNormalizer.Normalize(left, family, chain, addresses, services);
        if (a.IsFailure)
        {
            return a.Code;
        }

        PredicateAlgebraResult b = PredicateNormalizer.Normalize(right, family, chain, addresses, services);
        if (b.IsFailure)
        {
            return b.Code;
        }

        return PredicateAlgebra.Overlaps(a.Value!, b.Value!)
            ? PolicyExceptionCodes.Overlap
            : null;
    }

    private static string? CheckTcpFlags(TcpFlagConstraint? exception, TcpFlagConstraint? target)
    {
        if (target is null)
        {
            return null;
        }

        if (exception is null)
        {
            return PolicyExceptionCodes.NotSubset;
        }

        return SameSet(exception.RequiredPresent, target.RequiredPresent)
               && SameSet(exception.RequiredAbsent, target.RequiredAbsent)
            ? null
            : PolicyExceptionCodes.NotSubset;
    }

    private static string? CheckIpsec(IpsecPolicyPredicate? exception, IpsecPolicyPredicate? target)
    {
        if (target is null)
        {
            return null;
        }

        if (exception is null)
        {
            return PolicyExceptionCodes.NotSubset;
        }

        return exception.Direction == target.Direction && exception.Policy == target.Policy
            ? null
            : PolicyExceptionCodes.NotSubset;
    }

    private static bool IsUnconstrainedAddress(AddressSelector? selector)
        => selector is null || selector.UsesUniverseInclude;

    private static bool SameSet<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : struct, Enum
        => left.Count == right.Count && left.ToHashSet().SetEquals(right);
}
