using Mfc.Application.Models;

namespace Mfc.Application.Policies;

/// <summary>Stable idempotency payload fragments for rule mutate commands (LOCK-6).</summary>
internal static class PolicyRuleIdempotency
{
    public static object HashPredicate(TrafficPredicateInput? predicate)
    {
        if (predicate is null)
        {
            return new { empty = true };
        }

        return new
        {
            source_include = Sorted(predicate.SourceAddresses?.Include),
            source_exclude = Sorted(predicate.SourceAddresses?.Exclude),
            dest_include = Sorted(predicate.DestinationAddresses?.Include),
            dest_exclude = Sorted(predicate.DestinationAddresses?.Exclude),
            ingress_include = Sorted(predicate.IngressZones?.Include),
            ingress_exclude = Sorted(predicate.IngressZones?.Exclude),
            egress_include = Sorted(predicate.EgressZones?.Include),
            egress_exclude = Sorted(predicate.EgressZones?.Exclude),
            services = Sorted(predicate.Services?.Include),
            connection_states = SortedEnums(predicate.ConnectionStates),
            connection_nat_states = SortedEnums(predicate.ConnectionNatStates),
            source_address_types = SortedEnums(predicate.SourceAddressTypes),
            destination_address_types = SortedEnums(predicate.DestinationAddressTypes),
            tcp_present = SortedEnums(predicate.TcpFlags?.RequiredPresent),
            tcp_absent = SortedEnums(predicate.TcpFlags?.RequiredAbsent),
            ipsec = predicate.IpsecPolicy is null
                ? null
                : new
                {
                    direction = predicate.IpsecPolicy.Direction.ToString(),
                    policy = predicate.IpsecPolicy.Policy.ToString(),
                },
        };
    }

    public static object HashLogging(LogSpecificationInput? logging)
        => logging is null
            ? new { empty = true }
            : new { logging.Enabled, prefix = logging.Prefix ?? string.Empty };

    private static string[] Sorted(IReadOnlyList<Guid>? ids)
        => (ids ?? []).Select(static id => id.ToString("D")).OrderBy(static s => s, StringComparer.Ordinal).ToArray();

    private static string[] SortedEnums<T>(IReadOnlyList<T>? values)
        where T : struct, Enum
        => (values ?? []).Select(static v => v.ToString()!).OrderBy(static s => s, StringComparer.Ordinal).ToArray();
}
