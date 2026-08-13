using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Discovery;

/// <summary>Validation finding produced while resolving interface-list membership.</summary>
public sealed class DiscoveryFinding
{
    public const string InterfaceListCycle = InterfaceListMembershipFinding.InterfaceListCycle;
    public const string MissingInterfaceReference = InterfaceListMembershipFinding.MissingInterfaceReference;
    public const string MissingListReference = InterfaceListMembershipFinding.MissingListReference;
    public const string InvalidCidr = "INVALID_CIDR";
    public const string MissingRoutingTableReference = "MISSING_ROUTING_TABLE_REFERENCE";
    public const string UnsupportedForEditing = "UNSUPPORTED_FOR_EDITING";
    public const string VrrpRoleInconsistent = "VRRP_ROLE_INCONSISTENT";
    public const string InvalidVrrpVrid = "INVALID_VRRP_VRID";
    public const string UnknownSwitchChip = "UNKNOWN_SWITCH_CHIP";
    public const string MissingVethReference = "MISSING_VETH_REFERENCE";
    public const string MissingVrfInterfaceReference = "MISSING_VRF_INTERFACE_REFERENCE";
    public const string SharedVethMultiEndpoint = "SHARED_VETH_MULTI_ENDPOINT";
    public const string PacketPathBypassesIpFirewall = "PACKET_PATH_BYPASSES_IP_FIREWALL";
    public const string PacketPathNotProven = "PACKET_PATH_NOT_PROVEN";

    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Input list definition for membership resolution (RouterOS discovery adapter).</summary>
public sealed class InterfaceListSpec
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> Include { get; init; }

    public required IReadOnlyList<string> Exclude { get; init; }
}

/// <summary>Explicit <c>/interface/list/member</c> row.</summary>
public sealed class InterfaceListMemberSpec
{
    public required string List { get; init; }

    public required string Interface { get; init; }

    public required bool Disabled { get; init; }
}

/// <summary>Resolved membership for one interface list.</summary>
public sealed class ResolvedInterfaceListMembership
{
    public required string ListName { get; init; }

    /// <summary>Deterministic ordinal-sorted interface names.</summary>
    public required IReadOnlyList<string> Members { get; init; }

    public required bool HasCycle { get; init; }
}

/// <summary>
/// Thin RouterOS discovery adapter that delegates to Domain <see cref="InterfaceListMembership"/>.
/// </summary>
public static class InterfaceListMembershipResolver
{
    public static IReadOnlyList<ResolvedInterfaceListMembership> Resolve(
        IEnumerable<InterfaceListSpec> lists,
        IEnumerable<InterfaceListMemberSpec> members,
        IReadOnlySet<string> knownInterfaces,
        out IReadOnlyList<DiscoveryFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(knownInterfaces);

        IReadOnlyList<Mfc.Domain.Policy.ResolvedInterfaceListMembership> domainResolved =
            InterfaceListMembership.Resolve(
                lists.Select(l => new Mfc.Domain.Policy.InterfaceListSpec
                {
                    Name = l.Name,
                    Include = l.Include,
                    Exclude = l.Exclude,
                }),
                members.Select(m => new Mfc.Domain.Policy.InterfaceListMemberSpec
                {
                    List = m.List,
                    Interface = m.Interface,
                    Disabled = m.Disabled,
                }),
                knownInterfaces,
                out IReadOnlyList<InterfaceListMembershipFinding> domainFindings);

        findings = domainFindings
            .Select(f => new DiscoveryFinding
            {
                Code = f.Code,
                Message = f.Message,
                Subject = f.Subject,
            })
            .ToArray();

        return domainResolved
            .Select(r => new ResolvedInterfaceListMembership
            {
                ListName = r.ListName,
                Members = r.Members,
                HasCycle = r.HasCycle,
            })
            .ToArray();
    }
}
