using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Reads interfaces, IPv4/IPv6 addresses, and interface lists via the typed allowlist (M1-12).
/// Separates configuration from observations and resolves list membership deterministically.
/// </summary>
public static class InterfaceAddressDiscovery
{
    private static readonly char[] ListSeparators = [',', ';'];

    /// <summary>Discovers interface and address bindings from an open session.</summary>
    public static async Task<InterfaceAddressDiscoveryResult> DiscoverAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        List<string> warnings = [];

        RosReadCommandResult interfaces = await ExecuteAsync(
            session, RosReadCommandId.Interfaces, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult ipv4 = await ExecuteAsync(
            session, RosReadCommandId.Ipv4Addresses, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult ipv6 = await ExecuteAsync(
            session, RosReadCommandId.Ipv6Addresses, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult lists = await ExecuteAsync(
            session, RosReadCommandId.InterfaceLists, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult members = await ExecuteAsync(
            session, RosReadCommandId.InterfaceListMembers, warnings, cancellationToken).ConfigureAwait(false);

        return BuildResult(interfaces, ipv4, ipv6, lists, members, warnings);
    }

    /// <summary>
    /// Builds discovery result from already-executed command results (unit-testable without a session).
    /// </summary>
    public static InterfaceAddressDiscoveryResult BuildResult(
        RosReadCommandResult interfaces,
        RosReadCommandResult ipv4,
        RosReadCommandResult ipv6,
        RosReadCommandResult lists,
        RosReadCommandResult members,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(ipv4);
        ArgumentNullException.ThrowIfNull(ipv6);
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(members);

        List<DiscoveryFinding> findings = [];
        List<InterfaceDiscovery> mappedInterfaces = MapInterfaces(interfaces);
        HashSet<string> knownNames = mappedInterfaces
            .Select(i => i.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        (List<IpAddressDiscovery> v4Static, List<IpAddressDiscovery> v4Dynamic) =
            MapAddresses(ipv4, IpAddressFamilyKind.Ipv4, knownNames, findings);
        (List<IpAddressDiscovery> v6Static, List<IpAddressDiscovery> v6Dynamic) =
            MapAddresses(ipv6, IpAddressFamilyKind.Ipv6, knownNames, findings);

        List<InterfaceListDiscovery> mappedLists = MapLists(lists);
        List<InterfaceListMemberDiscovery> mappedMembers = MapMembers(members);

        IReadOnlyList<ResolvedInterfaceListMembership> resolved = InterfaceListMembershipResolver.Resolve(
            mappedLists.Select(l => new InterfaceListSpec
            {
                Name = l.Name ?? string.Empty,
                Include = l.Include,
                Exclude = l.Exclude,
            }).Where(l => l.Name.Length > 0),
            mappedMembers.Select(m => new InterfaceListMemberSpec
            {
                List = m.List ?? string.Empty,
                Interface = m.Interface ?? string.Empty,
                Disabled = IsTruthy(m.Disabled),
            }).Where(m => m.List.Length > 0 && m.Interface.Length > 0),
            knownNames,
            out IReadOnlyList<DiscoveryFinding> membershipFindings);
        findings.AddRange(membershipFindings);

        return new InterfaceAddressDiscoveryResult
        {
            Interfaces = mappedInterfaces
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .ToArray(),
            Ipv4StaticAddresses = v4Static
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .ThenBy(a => a.Interface, StringComparer.Ordinal)
                .ToArray(),
            Ipv4DynamicAddresses = v4Dynamic
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .ThenBy(a => a.Interface, StringComparer.Ordinal)
                .ToArray(),
            Ipv6StaticAddresses = v6Static
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .ThenBy(a => a.Interface, StringComparer.Ordinal)
                .ToArray(),
            Ipv6DynamicAddresses = v6Dynamic
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .ThenBy(a => a.Interface, StringComparer.Ordinal)
                .ToArray(),
            InterfaceLists = mappedLists
                .OrderBy(l => l.Name, StringComparer.Ordinal)
                .ToArray(),
            InterfaceListMembers = mappedMembers
                .OrderBy(m => m.List, StringComparer.Ordinal)
                .ThenBy(m => m.Interface, StringComparer.Ordinal)
                .ToArray(),
            ResolvedMembership = resolved,
            Findings = findings,
            Warnings = warnings?.ToArray() ?? [],
        };
    }

    private static async Task<RosReadCommandResult> ExecuteAsync(
        RosSession session,
        RosReadCommandId commandId,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            session,
            commandId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            warnings.Add($"{commandId}: {result.Error?.Code} {result.Error?.Message}");
        }

        return result;
    }

    private static List<InterfaceDiscovery> MapInterfaces(RosReadCommandResult result)
    {
        List<InterfaceDiscovery> items = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            items.Add(new InterfaceDiscovery
            {
                Id = Get(row, ".id"),
                Name = Get(row, "name"),
                DefaultName = Get(row, "default-name"),
                Type = Get(row, "type"),
                Mtu = Get(row, "mtu"),
                MacAddress = Get(row, "mac-address"),
                Disabled = Get(row, "disabled"),
                Comment = Get(row, "comment"),
                Running = Get(row, "running"),
                ActualMtu = Get(row, "actual-mtu"),
                Dynamic = Get(row, "dynamic"),
                Slave = Get(row, "slave"),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static (List<IpAddressDiscovery> Static, List<IpAddressDiscovery> Dynamic) MapAddresses(
        RosReadCommandResult result,
        IpAddressFamilyKind family,
        HashSet<string> knownInterfaces,
        List<DiscoveryFinding> findings)
    {
        List<IpAddressDiscovery> staticRows = [];
        List<IpAddressDiscovery> dynamicRows = [];
        foreach (RosReadRecord row in result.Records)
        {
            string? rawAddress = Get(row, "address");
            bool normalized;
            string? cidr;
            if (family == IpAddressFamilyKind.Ipv4)
            {
                normalized = CidrNormalizer.TryNormalizeIpv4(rawAddress, out string n, out string? error);
                cidr = normalized ? n : rawAddress;
                if (!normalized && rawAddress is not null)
                {
                    findings.Add(new DiscoveryFinding
                    {
                        Code = DiscoveryFinding.InvalidCidr,
                        Message = error ?? "Invalid IPv4 CIDR.",
                        Subject = rawAddress,
                    });
                }
            }
            else
            {
                normalized = CidrNormalizer.TryNormalizeIpv6(rawAddress, out string n, out string? error);
                cidr = normalized ? n : rawAddress;
                if (!normalized && rawAddress is not null)
                {
                    findings.Add(new DiscoveryFinding
                    {
                        Code = DiscoveryFinding.InvalidCidr,
                        Message = error ?? "Invalid IPv6 CIDR.",
                        Subject = rawAddress,
                    });
                }
            }

            string? network = Get(row, "network");
            if (family == IpAddressFamilyKind.Ipv4
                && network is not null
                && CidrNormalizer.TryNormalizeIpv4(
                    network.Contains('/', StringComparison.Ordinal) ? network : network + "/32",
                    out string normalizedNetwork,
                    out _))
            {
                // Keep network as host/network token without forcing /32 when RouterOS omits prefix.
                network = network.Contains('/', StringComparison.Ordinal)
                    ? normalizedNetwork
                    : normalizedNetwork.Split('/')[0];
            }

            string? iface = Get(row, "interface");
            if (!string.IsNullOrWhiteSpace(iface) && !knownInterfaces.Contains(iface))
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.MissingInterfaceReference,
                    Message = $"{family} address references unknown interface '{iface}'.",
                    Subject = iface,
                });
            }

            bool isDynamic = IsTruthy(Get(row, "dynamic"));
            IpAddressDiscovery mapped = new()
            {
                Family = family,
                Id = Get(row, ".id"),
                AddressCidr = cidr,
                AddressCidrRaw = rawAddress,
                AddressNormalized = normalized,
                Network = network,
                Interface = iface,
                Disabled = Get(row, "disabled"),
                Comment = Get(row, "comment"),
                FromPool = Get(row, "from-pool"),
                IsDynamic = isDynamic,
                ActualInterface = Get(row, "actual-interface"),
                RawProperties = row.RawProperties,
            };

            if (isDynamic)
            {
                dynamicRows.Add(mapped);
            }
            else
            {
                staticRows.Add(mapped);
            }
        }

        return (staticRows, dynamicRows);
    }

    private static List<InterfaceListDiscovery> MapLists(RosReadCommandResult result)
    {
        List<InterfaceListDiscovery> items = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            items.Add(new InterfaceListDiscovery
            {
                Id = Get(row, ".id"),
                Name = Get(row, "name"),
                Include = SplitList(Get(row, "include")),
                Exclude = SplitList(Get(row, "exclude")),
                Comment = Get(row, "comment"),
                Dynamic = Get(row, "dynamic"),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static List<InterfaceListMemberDiscovery> MapMembers(RosReadCommandResult result)
    {
        List<InterfaceListMemberDiscovery> items = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            items.Add(new InterfaceListMemberDiscovery
            {
                Id = Get(row, ".id"),
                List = Get(row, "list"),
                Interface = Get(row, "interface"),
                Disabled = Get(row, "disabled"),
                IsDynamic = IsTruthy(Get(row, "dynamic")),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static string[] SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? Get(RosReadRecord row, string name)
        => row.KnownProperties.TryGetValue(name, out string? value) ? value : null;
}
