using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Fail-closed structural subset / overlap proofs for exception predicates (M2-08 LOCK-3′).
/// Interval/port algebra is out of scope (M2-09 residual).
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
    /// Returns a <c>POLICY_EXCEPTION_*</c> code when <paramref name="exception"/> is not a
    /// fail-closed structural subset of <paramref name="target"/>; otherwise null.
    /// </summary>
    public static string? CheckSubset(TrafficPredicate exception, TrafficPredicate target)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(target);
        if (IsAddressUniverse(target))
        {
            return PolicyExceptionCodes.UniverseTarget;
        }

        return CheckAddress(exception.SourceAddresses, target.SourceAddresses)
               ?? CheckAddress(exception.DestinationAddresses, target.DestinationAddresses)
               ?? CheckZones(exception.IngressZones, target.IngressZones)
               ?? CheckZones(exception.EgressZones, target.EgressZones)
               ?? CheckServices(exception.Services, target.Services)
               ?? CheckEnums(exception.ConnectionStates, target.ConnectionStates)
               ?? CheckEnums(exception.ConnectionNatStates, target.ConnectionNatStates)
               ?? CheckEnums(exception.SourceAddressTypes, target.SourceAddressTypes)
               ?? CheckEnums(exception.DestinationAddressTypes, target.DestinationAddressTypes)
               ?? CheckTcpFlags(exception.TcpFlags, target.TcpFlags)
               ?? CheckIpsec(exception.IpsecPolicy, target.IpsecPolicy);
    }

    /// <summary>
    /// True when no dimension has disjoint nonempty UUID/enum includes (AC#6).
    /// UUID-disjoint success is a named M2-09 residual (interval overlap possible).
    /// </summary>
    public static bool StructurallyOverlaps(TrafficPredicate left, TrafficPredicate right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (DisjointAddresses(left.SourceAddresses, right.SourceAddresses))
        {
            return false;
        }

        if (DisjointAddresses(left.DestinationAddresses, right.DestinationAddresses))
        {
            return false;
        }

        if (DisjointZones(left.IngressZones, right.IngressZones))
        {
            return false;
        }

        if (DisjointZones(left.EgressZones, right.EgressZones))
        {
            return false;
        }

        if (DisjointServices(left.Services, right.Services))
        {
            return false;
        }

        if (DisjointEnums(left.ConnectionStates, right.ConnectionStates))
        {
            return false;
        }

        if (DisjointEnums(left.ConnectionNatStates, right.ConnectionNatStates))
        {
            return false;
        }

        if (DisjointEnums(left.SourceAddressTypes, right.SourceAddressTypes))
        {
            return false;
        }

        return !DisjointEnums(left.DestinationAddressTypes, right.DestinationAddressTypes);
    }

    private static string? CheckAddress(AddressSelector? exception, AddressSelector? target)
    {
        if (IsUnconstrainedAddress(target))
        {
            return null;
        }

        if (IsUnconstrainedAddress(exception))
        {
            return PolicyExceptionCodes.NotSubset;
        }

        HashSet<Guid> targetInclude = target!.Include.Select(static id => id.Value).ToHashSet();
        if (exception!.Include.Any(id => !targetInclude.Contains(id.Value)))
        {
            return PolicyExceptionCodes.NotSubset;
        }

        HashSet<Guid> targetExclude = target.Exclude.Select(static id => id.Value).ToHashSet();
        HashSet<Guid> exceptionExclude = exception.Exclude.Select(static id => id.Value).ToHashSet();
        return targetExclude.Any(id => !exceptionExclude.Contains(id))
            ? PolicyExceptionCodes.NotSubset
            : null;
    }

    private static string? CheckZones(ZoneSelector? exception, ZoneSelector? target)
    {
        if (IsUnconstrainedZones(target))
        {
            return null;
        }

        if (IsUnconstrainedZones(exception))
        {
            return PolicyExceptionCodes.NotSubset;
        }

        HashSet<Guid> targetInclude = target!.Include.Select(static id => id.Value).ToHashSet();
        if (exception!.Include.Any(id => !targetInclude.Contains(id.Value)))
        {
            return PolicyExceptionCodes.NotSubset;
        }

        HashSet<Guid> targetExclude = target.Exclude.Select(static id => id.Value).ToHashSet();
        HashSet<Guid> exceptionExclude = exception.Exclude.Select(static id => id.Value).ToHashSet();
        return targetExclude.Any(id => !exceptionExclude.Contains(id))
            ? PolicyExceptionCodes.NotSubset
            : null;
    }

    private static string? CheckServices(ServiceSelector? exception, ServiceSelector? target)
    {
        if (IsUnconstrainedServices(target))
        {
            return null;
        }

        if (IsUnconstrainedServices(exception))
        {
            return PolicyExceptionCodes.NotSubset;
        }

        HashSet<Guid> targetInclude = target!.Include.Select(static id => id.Value).ToHashSet();
        return exception!.Include.Any(id => !targetInclude.Contains(id.Value))
            ? PolicyExceptionCodes.NotSubset
            : null;
    }

    private static string? CheckEnums<T>(IReadOnlyList<T>? exception, IReadOnlyList<T>? target)
        where T : struct, Enum
    {
        if (target is null)
        {
            return null;
        }

        if (exception is null)
        {
            return PolicyExceptionCodes.NotSubset;
        }

        HashSet<T> allowed = [.. target];
        return exception.Any(value => !allowed.Contains(value))
            ? PolicyExceptionCodes.NotSubset
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

    private static bool DisjointAddresses(AddressSelector? left, AddressSelector? right)
    {
        if (IsUnconstrainedAddress(left) || IsUnconstrainedAddress(right))
        {
            return false;
        }

        HashSet<Guid> leftIds = left!.Include.Select(static id => id.Value).ToHashSet();
        return !leftIds.Overlaps(right!.Include.Select(static id => id.Value));
    }

    private static bool DisjointZones(ZoneSelector? left, ZoneSelector? right)
    {
        if (IsUnconstrainedZones(left) || IsUnconstrainedZones(right))
        {
            return false;
        }

        HashSet<Guid> leftIds = left!.Include.Select(static id => id.Value).ToHashSet();
        return !leftIds.Overlaps(right!.Include.Select(static id => id.Value));
    }

    private static bool DisjointServices(ServiceSelector? left, ServiceSelector? right)
    {
        if (IsUnconstrainedServices(left) || IsUnconstrainedServices(right))
        {
            return false;
        }

        HashSet<Guid> leftIds = left!.Include.Select(static id => id.Value).ToHashSet();
        return !leftIds.Overlaps(right!.Include.Select(static id => id.Value));
    }

    private static bool DisjointEnums<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        where T : struct, Enum
    {
        if (left is null || right is null)
        {
            return false;
        }

        HashSet<T> leftSet = [.. left];
        return !leftSet.Overlaps(right);
    }

    private static bool IsUnconstrainedAddress(AddressSelector? selector)
        => selector is null || selector.UsesUniverseInclude;

    private static bool IsUnconstrainedZones(ZoneSelector? selector)
        => selector is null || selector.Include.Count == 0;

    private static bool IsUnconstrainedServices(ServiceSelector? selector)
        => selector is null || selector.MatchesAnyProtocol;

    private static bool SameSet<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : struct, Enum
        => left.Count == right.Count && left.ToHashSet().SetEquals(right);
}
