namespace Mfc.RouterOs.Discovery;

/// <summary>Configuration vs observation split for a discovered interface.</summary>
public sealed class InterfaceDiscovery
{
    public required string? Id { get; init; }

    public required string? Name { get; init; }

    public required string? DefaultName { get; init; }

    public required string? Type { get; init; }

    public required string? Mtu { get; init; }

    public required string? MacAddress { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    /// <summary>Runtime link state — excluded from configuration hash material.</summary>
    public required string? Running { get; init; }

    public required string? ActualMtu { get; init; }

    public required string? Dynamic { get; init; }

    public required string? Slave { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public enum IpAddressFamilyKind : byte
{
    Ipv4 = 4,
    Ipv6 = 6,
}

/// <summary>One IPv4 or IPv6 address binding. Families are never mixed in one collection.</summary>
public sealed class IpAddressDiscovery
{
    public required IpAddressFamilyKind Family { get; init; }

    public required string? Id { get; init; }

    /// <summary>Normalized CIDR when parse succeeded; otherwise raw RouterOS value.</summary>
    public required string? AddressCidr { get; init; }

    public required string? AddressCidrRaw { get; init; }

    public required bool AddressNormalized { get; init; }

    public required string? Network { get; init; }

    public required string? Interface { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required string? FromPool { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? ActualInterface { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class InterfaceListDiscovery
{
    public required string? Id { get; init; }

    public required string? Name { get; init; }

    public required IReadOnlyList<string> Include { get; init; }

    public required IReadOnlyList<string> Exclude { get; init; }

    public required string? Comment { get; init; }

    public required string? Dynamic { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class InterfaceListMemberDiscovery
{
    public required string? Id { get; init; }

    public required string? List { get; init; }

    public required string? Interface { get; init; }

    public required string? Disabled { get; init; }

    public required bool IsDynamic { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Aggregate interface/address discovery result (M1-12).</summary>
public sealed class InterfaceAddressDiscoveryResult
{
    public required IReadOnlyList<InterfaceDiscovery> Interfaces { get; init; }

    public required IReadOnlyList<IpAddressDiscovery> Ipv4StaticAddresses { get; init; }

    public required IReadOnlyList<IpAddressDiscovery> Ipv4DynamicAddresses { get; init; }

    public required IReadOnlyList<IpAddressDiscovery> Ipv6StaticAddresses { get; init; }

    public required IReadOnlyList<IpAddressDiscovery> Ipv6DynamicAddresses { get; init; }

    public required IReadOnlyList<InterfaceListDiscovery> InterfaceLists { get; init; }

    public required IReadOnlyList<InterfaceListMemberDiscovery> InterfaceListMembers { get; init; }

    public required IReadOnlyList<ResolvedInterfaceListMembership> ResolvedMembership { get; init; }

    public required IReadOnlyList<DiscoveryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Configuration hash material. Excludes runtime <c>running</c> and dynamic-only address rows.
    /// </summary>
    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (InterfaceDiscovery iface in Interfaces.OrderBy(i => i.Name, StringComparer.Ordinal))
            {
                string key = iface.Name ?? iface.Id ?? "unknown";
                Put(material, $"iface.{key}.type", iface.Type);
                Put(material, $"iface.{key}.mtu", iface.Mtu);
                Put(material, $"iface.{key}.mac", iface.MacAddress);
                Put(material, $"iface.{key}.disabled", iface.Disabled);
                // Intentionally omit Running / ActualMtu.
            }

            foreach (IpAddressDiscovery address in Ipv4StaticAddresses
                         .Concat(Ipv6StaticAddresses)
                         .OrderBy(a => a.Family)
                         .ThenBy(a => a.AddressCidr, StringComparer.Ordinal)
                         .ThenBy(a => a.Interface, StringComparer.Ordinal))
            {
                string key = $"{(int)address.Family}:{address.AddressCidr}:{address.Interface}";
                Put(material, $"addr.{key}.disabled", address.Disabled);
                Put(material, $"addr.{key}.network", address.Network);
            }

            foreach (ResolvedInterfaceListMembership membership in ResolvedMembership
                         .OrderBy(m => m.ListName, StringComparer.Ordinal))
            {
                Put(
                    material,
                    $"list.{membership.ListName}.members",
                    string.Join(',', membership.Members));
            }

            return material;
        }
    }

    private static void Put(Dictionary<string, string> target, string key, string? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }
}
