using System.Security.Cryptography;
using System.Text;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Reads IPv4/IPv6 firewall filters and address lists via the typed allowlist (M1-13).
/// Preserves effective rule order, separates static/dynamic material, and digests dynamic lists.
/// </summary>
public static class FirewallFilterDiscovery
{
    /// <summary>Discovers filter rules and address lists from an open session.</summary>
    public static async Task<FirewallFilterDiscoveryResult> DiscoverAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        List<string> warnings = [];

        RosReadCommandResult ipv4Filter = await ExecuteAsync(
            session, RosReadCommandId.Ipv4Filter, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult ipv6Filter = await ExecuteAsync(
            session, RosReadCommandId.Ipv6Filter, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult ipv4Lists = await ExecuteAsync(
            session, RosReadCommandId.Ipv4AddressLists, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult ipv6Lists = await ExecuteAsync(
            session, RosReadCommandId.Ipv6AddressLists, warnings, cancellationToken).ConfigureAwait(false);

        return BuildResult(ipv4Filter, ipv6Filter, ipv4Lists, ipv6Lists, warnings);
    }

    /// <summary>Builds discovery result from executed command results (unit-testable).</summary>
    public static FirewallFilterDiscoveryResult BuildResult(
        RosReadCommandResult ipv4Filter,
        RosReadCommandResult ipv6Filter,
        RosReadCommandResult ipv4AddressLists,
        RosReadCommandResult ipv6AddressLists,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(ipv4Filter);
        ArgumentNullException.ThrowIfNull(ipv6Filter);
        ArgumentNullException.ThrowIfNull(ipv4AddressLists);
        ArgumentNullException.ThrowIfNull(ipv6AddressLists);

        List<FirewallFilterRuleDiscovery> v4Rules = MapFilterRules(ipv4Filter, IpAddressFamilyKind.Ipv4);
        List<FirewallFilterRuleDiscovery> v6Rules = MapFilterRules(ipv6Filter, IpAddressFamilyKind.Ipv6);
        (List<FirewallAddressListEntryDiscovery> v4Static, List<DynamicAddressListSummary> v4Dyn) =
            MapAddressLists(ipv4AddressLists, IpAddressFamilyKind.Ipv4);
        (List<FirewallAddressListEntryDiscovery> v6Static, List<DynamicAddressListSummary> v6Dyn) =
            MapAddressLists(ipv6AddressLists, IpAddressFamilyKind.Ipv6);

        return new FirewallFilterDiscoveryResult
        {
            Ipv4FilterRules = v4Rules,
            Ipv6FilterRules = v6Rules,
            Ipv4StaticAddressListEntries = v4Static,
            Ipv6StaticAddressListEntries = v6Static,
            Ipv4DynamicAddressListSummaries = v4Dyn,
            Ipv6DynamicAddressListSummaries = v6Dyn,
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

    private static List<FirewallFilterRuleDiscovery> MapFilterRules(
        RosReadCommandResult result,
        IpAddressFamilyKind family)
    {
        List<FirewallFilterRuleDiscovery> rules = new(result.Records.Count);
        int staticOrdinal = 0;
        for (int effective = 0; effective < result.Records.Count; effective++)
        {
            RosReadRecord row = result.Records[effective];
            bool isDynamic = IsTruthy(Get(row, "dynamic"));
            string? comment = Get(row, "comment");
            bool hasMarker = FwcOwnershipMarker.TryRecognize(comment, out string? marker);

            // Defense: counters must never appear even if a future profile mistake requests them.
            Dictionary<string, string> known = row.KnownProperties
                .Where(kv => !IsCounterProperty(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            Dictionary<string, string> raw = row.RawProperties
                .Where(kv => !IsCounterProperty(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

            rules.Add(new FirewallFilterRuleDiscovery
            {
                Family = family,
                RouterOsRowId = Get(row, ".id"),
                EffectiveOrdinal = effective,
                StaticOrdinal = isDynamic ? null : staticOrdinal,
                IsDynamic = isDynamic,
                Chain = Get(row, "chain"),
                Action = Get(row, "action"),
                Disabled = Get(row, "disabled"),
                Comment = comment,
                FwcOwnershipMarker = marker,
                HasFwcOwnershipMarker = hasMarker,
                Protocol = Get(row, "protocol"),
                SrcAddress = Get(row, "src-address"),
                DstAddress = Get(row, "dst-address"),
                ConnectionState = Get(row, "connection-state"),
                HwOffload = Get(row, "hw-offload"),
                JumpTarget = Get(row, "jump-target"),
                RejectWith = Get(row, "reject-with"),
                AddressList = Get(row, "address-list"),
                AddressListTimeout = Get(row, "address-list-timeout"),
                Invalid = Get(row, "invalid"),
                KnownProperties = known,
                RawProperties = raw,
            });

            if (!isDynamic)
            {
                staticOrdinal++;
            }
        }

        return rules;
    }

    private static (List<FirewallAddressListEntryDiscovery> Static, List<DynamicAddressListSummary> Dynamic) MapAddressLists(
        RosReadCommandResult result,
        IpAddressFamilyKind family)
    {
        List<FirewallAddressListEntryDiscovery> staticEntries = [];
        Dictionary<string, List<string>> dynamicCanonicalByList = new(StringComparer.Ordinal);

        foreach (RosReadRecord row in result.Records)
        {
            string? list = Get(row, "list");
            string? address = Get(row, "address");
            string? timeout = Get(row, "timeout");
            bool isDynamicFlag = IsTruthy(Get(row, "dynamic"));
            bool hasTimeout = !string.IsNullOrWhiteSpace(timeout);
            bool treatAsDynamic = isDynamicFlag || hasTimeout;

            string? canonical = CanonicalizeAddressListEntry(address, family);

            if (treatAsDynamic)
            {
                string listName = list ?? string.Empty;
                if (!dynamicCanonicalByList.TryGetValue(listName, out List<string>? entries))
                {
                    entries = [];
                    dynamicCanonicalByList[listName] = entries;
                }

                entries.Add(canonical ?? address ?? string.Empty);
                continue;
            }

            staticEntries.Add(new FirewallAddressListEntryDiscovery
            {
                Family = family,
                RouterOsRowId = Get(row, ".id"),
                List = list,
                Address = address,
                AddressCanonical = canonical,
                Disabled = Get(row, "disabled"),
                Comment = Get(row, "comment"),
                RawProperties = row.RawProperties,
            });
        }

        List<DynamicAddressListSummary> summaries = [];
        foreach ((string listName, List<string> entries) in dynamicCanonicalByList
                     .OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            summaries.Add(BuildDynamicSummary(listName, family, entries));
        }

        staticEntries.Sort((a, b) =>
        {
            int byList = string.Compare(a.List, b.List, StringComparison.Ordinal);
            if (byList != 0)
            {
                return byList;
            }

            return string.Compare(
                a.AddressCanonical ?? a.Address,
                b.AddressCanonical ?? b.Address,
                StringComparison.Ordinal);
        });

        return (staticEntries, summaries);
    }

    private static DynamicAddressListSummary BuildDynamicSummary(
        string listName,
        IpAddressFamilyKind family,
        List<string> canonicalEntries)
    {
        List<byte[]> entryDigests = new(canonicalEntries.Count);
        foreach (string entry in canonicalEntries)
        {
            entryDigests.Add(SHA256.HashData(Encoding.UTF8.GetBytes(entry)));
        }

        entryDigests.Sort(static (a, b) => a.AsSpan().SequenceCompareTo(b));
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (byte[] digest in entryDigests)
        {
            hasher.AppendData(digest);
        }

        byte[] sortedDigest = hasher.GetHashAndReset();
        return new DynamicAddressListSummary
        {
            ListName = listName,
            Family = family,
            EntryCount = canonicalEntries.Count,
            SortedEntryDigestHex = Convert.ToHexString(sortedDigest).ToLowerInvariant(),
        };
    }

    private static string? CanonicalizeAddressListEntry(string? address, IpAddressFamilyKind family)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        if (address.Contains('/', StringComparison.Ordinal))
        {
            if (family == IpAddressFamilyKind.Ipv4
                && CidrNormalizer.TryNormalizeIpv4(address, out string v4, out _))
            {
                return v4;
            }

            if (family == IpAddressFamilyKind.Ipv6
                && CidrNormalizer.TryNormalizeIpv6(address, out string v6, out _))
            {
                return v6;
            }
        }

        return address.Trim();
    }

    private static bool IsCounterProperty(string name)
        => string.Equals(name, "bytes", StringComparison.Ordinal)
           || string.Equals(name, "packets", StringComparison.Ordinal);

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? Get(RosReadRecord row, string name)
        => row.KnownProperties.TryGetValue(name, out string? value) ? value : null;
}
