using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>Maps rule command inputs onto Domain types.</summary>
internal static class PolicyRuleFactory
{
    public static TrafficPredicate ToPredicate(TrafficPredicateInput? input)
    {
        if (input is null)
        {
            return TrafficPredicate.Create();
        }

        return TrafficPredicate.Create(
            sourceAddresses: ToAddressSelector(input.SourceAddresses),
            destinationAddresses: ToAddressSelector(input.DestinationAddresses),
            ingressZones: ToZoneSelector(input.IngressZones),
            egressZones: ToZoneSelector(input.EgressZones),
            services: ToServiceSelector(input.Services),
            connectionStates: input.ConnectionStates,
            connectionNatStates: input.ConnectionNatStates,
            sourceAddressTypes: input.SourceAddressTypes,
            destinationAddressTypes: input.DestinationAddressTypes,
            tcpFlags: ToTcpFlags(input.TcpFlags),
            ipsecPolicy: ToIpsec(input.IpsecPolicy));
    }

    public static PolicyRule CreateRule(
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        uint ordinal,
        TrafficPredicateInput? predicate,
        RuleEffectInput effect,
        LogSpecificationInput? logging,
        bool enabled,
        bool exceptionEligible,
        string? description,
        RuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return PolicyRule.Create(
            family,
            chain,
            stage,
            ordinal,
            ToPredicate(predicate),
            RuleEffectSpec.Create(effect.Kind, effect.RejectMode),
            logging is null
                ? LogSpecification.Disabled
                : LogSpecification.Create(logging.Enabled, logging.Prefix),
            enabled,
            exceptionEligible,
            description,
            id);
    }

    private static AddressSelector? ToAddressSelector(AddressSelectorInput? input)
    {
        if (input is null)
        {
            return null;
        }

        return AddressSelector.Create(
            input.Include.Select(static g => new AddressObjectId(g)),
            input.Exclude.Select(static g => new AddressObjectId(g)));
    }

    private static ZoneSelector? ToZoneSelector(ZoneSelectorInput? input)
    {
        if (input is null)
        {
            return null;
        }

        return ZoneSelector.Create(
            input.Include.Select(static g => new ZoneId(g)),
            input.Exclude.Select(static g => new ZoneId(g)));
    }

    private static ServiceSelector? ToServiceSelector(ServiceSelectorInput? input)
    {
        if (input is null)
        {
            return null;
        }

        return ServiceSelector.Create(input.Include.Select(static g => new ServiceObjectId(g)));
    }

    private static TcpFlagConstraint? ToTcpFlags(TcpFlagConstraintInput? input)
    {
        if (input is null)
        {
            return null;
        }

        return TcpFlagConstraint.Create(input.RequiredPresent, input.RequiredAbsent);
    }

    private static IpsecPolicyPredicate? ToIpsec(IpsecPolicyPredicateInput? input)
    {
        if (input is null)
        {
            return null;
        }

        return IpsecPolicyPredicate.Create(input.Direction, input.Policy);
    }
}
