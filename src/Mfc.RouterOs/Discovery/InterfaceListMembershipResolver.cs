namespace Mfc.RouterOs.Discovery;

/// <summary>Validation finding produced while resolving interface-list membership.</summary>
public sealed class DiscoveryFinding
{
    public const string InterfaceListCycle = "INTERFACE_LIST_CYCLE";
    public const string MissingInterfaceReference = "MISSING_INTERFACE_REFERENCE";
    public const string MissingListReference = "MISSING_LIST_REFERENCE";
    public const string InvalidCidr = "INVALID_CIDR";
    public const string MissingRoutingTableReference = "MISSING_ROUTING_TABLE_REFERENCE";
    public const string UnsupportedForEditing = "UNSUPPORTED_FOR_EDITING";
    public const string VrrpRoleInconsistent = "VRRP_ROLE_INCONSISTENT";
    public const string InvalidVrrpVrid = "INVALID_VRRP_VRID";

    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Input list definition for membership resolution.</summary>
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
/// Deterministic interface-list membership resolution (Read Adapter Spec §23).
/// Order: include → exclude → explicit members. Reply order must not affect the result.
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

        Dictionary<string, InterfaceListSpec> listByName = lists
            .OrderBy(l => l.Name, StringComparer.Ordinal)
            .GroupBy(l => l.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        Dictionary<string, SortedSet<string>> explicitByList = new(StringComparer.Ordinal);
        List<DiscoveryFinding> found = [];

        foreach (InterfaceListMemberSpec member in members
                     .OrderBy(m => m.List, StringComparer.Ordinal)
                     .ThenBy(m => m.Interface, StringComparer.Ordinal))
        {
            if (member.Disabled || string.IsNullOrWhiteSpace(member.List) || string.IsNullOrWhiteSpace(member.Interface))
            {
                continue;
            }

            if (!knownInterfaces.Contains(member.Interface))
            {
                found.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.MissingInterfaceReference,
                    Message = $"Interface-list member references unknown interface '{member.Interface}'.",
                    Subject = member.Interface,
                });
            }

            if (!explicitByList.TryGetValue(member.List, out SortedSet<string>? set))
            {
                set = new SortedSet<string>(StringComparer.Ordinal);
                explicitByList[member.List] = set;
            }

            set.Add(member.Interface);
        }

        List<ResolvedInterfaceListMembership> resolved = [];
        foreach (string listName in listByName.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            HashSet<string> stack = new(StringComparer.Ordinal);
            SortedSet<string> membersSet = ResolveList(
                listName,
                listByName,
                explicitByList,
                stack,
                found,
                out bool hasCycle);
            resolved.Add(new ResolvedInterfaceListMembership
            {
                ListName = listName,
                Members = membersSet.ToArray(),
                HasCycle = hasCycle,
            });
        }

        findings = found;
        return resolved;
    }

    private static SortedSet<string> ResolveList(
        string listName,
        Dictionary<string, InterfaceListSpec> listByName,
        Dictionary<string, SortedSet<string>> explicitByList,
        HashSet<string> stack,
        List<DiscoveryFinding> findings,
        out bool hasCycle)
    {
        hasCycle = false;
        if (!listByName.TryGetValue(listName, out InterfaceListSpec? list))
        {
            findings.Add(new DiscoveryFinding
            {
                Code = DiscoveryFinding.MissingListReference,
                Message = $"Referenced interface list '{listName}' does not exist.",
                Subject = listName,
            });
            return [];
        }

        if (!stack.Add(listName))
        {
            hasCycle = true;
            findings.Add(new DiscoveryFinding
            {
                Code = DiscoveryFinding.InterfaceListCycle,
                Message = $"Interface list '{listName}' participates in an include/exclude cycle.",
                Subject = listName,
            });
            // Spec: cycle is not replaced by a silent empty success — finding is required.
            return [];
        }

        try
        {
            SortedSet<string> result = new(StringComparer.Ordinal);

            foreach (string include in list.Include.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(include))
                {
                    continue;
                }

                SortedSet<string> included = ResolveList(
                    include,
                    listByName,
                    explicitByList,
                    stack,
                    findings,
                    out bool includeCycle);
                hasCycle |= includeCycle;
                foreach (string name in included)
                {
                    result.Add(name);
                }
            }

            foreach (string exclude in list.Exclude.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(exclude))
                {
                    continue;
                }

                SortedSet<string> excluded = ResolveList(
                    exclude,
                    listByName,
                    explicitByList,
                    stack,
                    findings,
                    out bool excludeCycle);
                hasCycle |= excludeCycle;
                foreach (string name in excluded)
                {
                    result.Remove(name);
                }
            }

            if (explicitByList.TryGetValue(listName, out SortedSet<string>? explicitMembers))
            {
                foreach (string name in explicitMembers)
                {
                    result.Add(name);
                }
            }

            return result;
        }
        finally
        {
            stack.Remove(listName);
        }
    }
}
