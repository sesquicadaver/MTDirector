namespace Mfc.RouterOs.Discovery;

/// <summary>One firewall filter rule from an ordered /print reply.</summary>
public sealed class FirewallFilterRuleDiscovery
{
    public required IpAddressFamilyKind Family { get; init; }

    /// <summary>RouterOS row id — raw only, never used as persistent identity.</summary>
    public required string? RouterOsRowId { get; init; }

    public required int EffectiveOrdinal { get; init; }

    /// <summary>Ordinal among static rules only; null for dynamic rules.</summary>
    public required int? StaticOrdinal { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? Chain { get; init; }

    public required string? Action { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required string? FwcOwnershipMarker { get; init; }

    public required bool HasFwcOwnershipMarker { get; init; }

    public required string? Protocol { get; init; }

    public required string? SrcAddress { get; init; }

    public required string? DstAddress { get; init; }

    public required string? ConnectionState { get; init; }

    public required string? HwOffload { get; init; }

    public required string? JumpTarget { get; init; }

    public required string? RejectWith { get; init; }

    public required string? AddressList { get; init; }

    public required string? AddressListTimeout { get; init; }

    public required string? Invalid { get; init; }

    /// <summary>All known profile properties (including opaque matchers).</summary>
    public required IReadOnlyDictionary<string, string> KnownProperties { get; init; }

    /// <summary>Unknown matchers returned by RouterOS outside the property profile.</summary>
    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Static address-list entry retained in full.</summary>
public sealed class FirewallAddressListEntryDiscovery
{
    public required IpAddressFamilyKind Family { get; init; }

    public required string? RouterOsRowId { get; init; }

    public required string? List { get; init; }

    public required string? Address { get; init; }

    public required string? AddressCanonical { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>
/// Digest-only summary of dynamic/timeout address-list entries for one list (Spec §30.3).
/// Plaintext dynamic addresses are not retained.
/// </summary>
public sealed class DynamicAddressListSummary
{
    public required string ListName { get; init; }

    public required IpAddressFamilyKind Family { get; init; }

    public required int EntryCount { get; init; }

    /// <summary>Lowercase hex SHA-256 over sorted per-entry digests.</summary>
    public required string SortedEntryDigestHex { get; init; }
}

/// <summary>Aggregate firewall filter + address-list discovery (M1-13).</summary>
public sealed class FirewallFilterDiscoveryResult
{
    public required IReadOnlyList<FirewallFilterRuleDiscovery> Ipv4FilterRules { get; init; }

    public required IReadOnlyList<FirewallFilterRuleDiscovery> Ipv6FilterRules { get; init; }

    public required IReadOnlyList<FirewallAddressListEntryDiscovery> Ipv4StaticAddressListEntries { get; init; }

    public required IReadOnlyList<FirewallAddressListEntryDiscovery> Ipv6StaticAddressListEntries { get; init; }

    public required IReadOnlyList<DynamicAddressListSummary> Ipv4DynamicAddressListSummaries { get; init; }

    public required IReadOnlyList<DynamicAddressListSummary> Ipv6DynamicAddressListSummaries { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Configuration hash material: static rules by static_ordinal, static address-list entries,
    /// and dynamic-list digests. Counters and RouterOS .id are excluded.
    /// </summary>
    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            AppendStaticRules(material, "ipv4", Ipv4FilterRules);
            AppendStaticRules(material, "ipv6", Ipv6FilterRules);
            foreach (FirewallAddressListEntryDiscovery entry in Ipv4StaticAddressListEntries
                         .Concat(Ipv6StaticAddressListEntries)
                         .OrderBy(e => e.Family)
                         .ThenBy(e => e.List, StringComparer.Ordinal)
                         .ThenBy(e => e.AddressCanonical ?? e.Address, StringComparer.Ordinal))
            {
                string key = $"{(int)entry.Family}:{entry.List}:{entry.AddressCanonical ?? entry.Address}";
                Put(material, $"alist.{key}.disabled", entry.Disabled);
                Put(material, $"alist.{key}.comment", entry.Comment);
            }

            foreach (DynamicAddressListSummary summary in Ipv4DynamicAddressListSummaries
                         .Concat(Ipv6DynamicAddressListSummaries)
                         .OrderBy(s => s.Family)
                         .ThenBy(s => s.ListName, StringComparer.Ordinal))
            {
                Put(
                    material,
                    $"alist-dyn.{(int)summary.Family}:{summary.ListName}.digest",
                    summary.SortedEntryDigestHex);
                Put(
                    material,
                    $"alist-dyn.{(int)summary.Family}:{summary.ListName}.count",
                    summary.EntryCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return material;
        }
    }

    private static void AppendStaticRules(
        Dictionary<string, string> material,
        string familyKey,
        IReadOnlyList<FirewallFilterRuleDiscovery> rules)
    {
        foreach (FirewallFilterRuleDiscovery rule in rules.Where(r => !r.IsDynamic && r.StaticOrdinal is not null)
                     .OrderBy(r => r.StaticOrdinal))
        {
            string prefix = $"filter.{familyKey}.{rule.StaticOrdinal}";
            Put(material, $"{prefix}.chain", rule.Chain);
            Put(material, $"{prefix}.action", rule.Action);
            Put(material, $"{prefix}.disabled", rule.Disabled);
            Put(material, $"{prefix}.comment", rule.Comment);
            Put(material, $"{prefix}.protocol", rule.Protocol);
            Put(material, $"{prefix}.src-address", rule.SrcAddress);
            Put(material, $"{prefix}.dst-address", rule.DstAddress);
            Put(material, $"{prefix}.connection-state", rule.ConnectionState);
            Put(material, $"{prefix}.hw-offload", rule.HwOffload);
            Put(material, $"{prefix}.jump-target", rule.JumpTarget);
            Put(material, $"{prefix}.reject-with", rule.RejectWith);
            Put(material, $"{prefix}.address-list", rule.AddressList);
            // Never include RouterOsRowId or bytes/packets.
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
