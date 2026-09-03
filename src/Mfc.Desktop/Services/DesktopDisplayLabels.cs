using System.Globalization;
using System.Text;

namespace Mfc.Desktop.Services;

/// <summary>
/// Operator-facing labels for Desktop: MikroTik / Winbox terminology instead of MFC wire keys.
/// Wire-format properties stay on export/clipboard; UI binds friendly text from here.
/// </summary>
public static class DesktopDisplayLabels
{
    /// <summary>MikroTik menu path for a captured section id.</summary>
    public static string FormatSectionTitle(string sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return "—";
        }

        if (SectionTitles.TryGetValue(sectionId, out string? title))
        {
            return title;
        }

        return FallbackSectionTitle(sectionId);
    }

    /// <summary>Winbox-style label for a RouterOS property name.</summary>
    public static string FormatPropertyName(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return "—";
        }

        if (PropertyLabels.TryGetValue(propertyName, out string? label))
        {
            return label;
        }

        return FallbackPropertyLabel(propertyName);
    }

    /// <summary>Operator line for one field, e.g. <c>Src. Address: 192.168.88.0/24</c>.</summary>
    public static string FormatPropertyLine(string propertyName, string value)
        => $"{FormatPropertyName(propertyName)}: {value}";

    /// <summary>Compact summary fragment for list rows.</summary>
    public static string FormatPropertyPair(string propertyName, string value)
        => FormatPropertyLine(propertyName, value);

    /// <summary>Human label for MFC metadata / hash keys shown in panels.</summary>
    public static string FormatMetadataLabel(string internalKey)
    {
        if (string.IsNullOrWhiteSpace(internalKey))
        {
            return "—";
        }

        if (MetadataLabels.TryGetValue(internalKey, out string? label))
        {
            return label;
        }

        return FallbackPropertyLabel(internalKey.Replace('_', '-'));
    }

    /// <summary>Re-label a diff summary that may be <c>name=value</c> or already friendly.</summary>
    public static string FormatDiffFieldSummary(string fieldName, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return FormatPropertyName(fieldName);
        }

        int eq = summary.IndexOf('=');
        if (eq > 0 && eq < summary.Length - 1)
        {
            string name = summary[..eq];
            string value = summary[(eq + 1)..];
            return FormatPropertyLine(name, value);
        }

        return summary;
    }

    public static string FormatRecordSummaryFriendly(
        string stableKey,
        string ordinalText,
        IReadOnlyList<SnapshotFieldLine> fields,
        bool hasMoreFields)
    {
        ArgumentNullException.ThrowIfNull(stableKey);
        ArgumentNullException.ThrowIfNull(ordinalText);
        ArgumentNullException.ThrowIfNull(fields);
        string compact = string.Join(
            "; ",
            fields.Take(4).Select(static f => FormatPropertyPair(f.Name, f.Value)));
        string suffix = hasMoreFields ? " …" : string.Empty;
        if (string.IsNullOrWhiteSpace(compact))
        {
            if (SnapshotPresentationIdentity.IsFingerprintKey(stableKey))
            {
                return HasUsefulOrdinal(ordinalText) ? "#" + ordinalText : "—";
            }

            return stableKey;
        }

        if (SnapshotPresentationIdentity.IsFingerprintKey(stableKey))
        {
            string prefix = HasUsefulOrdinal(ordinalText) ? "#" + ordinalText + " · " : string.Empty;
            return prefix + compact + suffix;
        }

        return $"{stableKey} · {compact}{suffix}";
    }

    public static string FormatDiffIdentityFriendly(
        string recordKey,
        string ordinalText,
        IReadOnlyList<SnapshotDiffFieldLine> fieldLines)
    {
        ArgumentNullException.ThrowIfNull(recordKey);
        ArgumentNullException.ThrowIfNull(ordinalText);
        ArgumentNullException.ThrowIfNull(fieldLines);
        if (!SnapshotPresentationIdentity.IsFingerprintKey(recordKey))
        {
            return string.IsNullOrWhiteSpace(recordKey) ? "—" : recordKey;
        }

        string fromFields = string.Join(
            "; ",
            fieldLines.Take(4).Select(static f => FormatDiffFieldSummary(f.FieldName, f.Summary)));
        if (!string.IsNullOrWhiteSpace(fromFields))
        {
            return fromFields;
        }

        return HasUsefulOrdinal(ordinalText) ? ordinalText : "Unmanaged record";
    }

    private static bool HasUsefulOrdinal(string ordinalText)
        => !string.IsNullOrWhiteSpace(ordinalText)
           && !string.Equals(ordinalText, "—", StringComparison.Ordinal)
           && !ordinalText.StartsWith("order: —", StringComparison.Ordinal);

    private static string FallbackSectionTitle(string sectionId)
    {
        string[] parts = sectionId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return sectionId;
        }

        StringBuilder builder = new();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(" → ");
            }

            builder.Append(TitleCaseSegment(parts[i]));
        }

        return builder.ToString();
    }

    private static string FallbackPropertyLabel(string propertyName)
    {
        if (propertyName.Contains('.', StringComparison.Ordinal))
        {
            return propertyName;
        }

        string[] tokens = propertyName.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return propertyName;
        }

        return string.Join(' ', tokens.Select(TitleCaseSegment));
    }

    private static string TitleCaseSegment(string segment)
    {
        if (string.Equals(segment, "ipv4", StringComparison.OrdinalIgnoreCase))
        {
            return "IPv4";
        }

        if (string.Equals(segment, "ipv6", StringComparison.OrdinalIgnoreCase))
        {
            return "IPv6";
        }

        if (string.Equals(segment, "vrrp", StringComparison.OrdinalIgnoreCase))
        {
            return "VRRP";
        }

        if (string.Equals(segment, "vrf", StringComparison.OrdinalIgnoreCase))
        {
            return "VRF";
        }

        if (segment.Length == 0)
        {
            return segment;
        }

        return char.ToUpper(segment[0], CultureInfo.InvariantCulture)
               + (segment.Length > 1 ? segment[1..] : string.Empty);
    }

    private static readonly Dictionary<string, string> SectionTitles =
        new(StringComparer.Ordinal)
        {
            ["firewall.ipv4.filter"] = "IP → Firewall → Filter Rules",
            ["firewall.ipv6.filter"] = "IPv6 → Firewall → Filter Rules",
            ["firewall.ipv4.nat"] = "IP → Firewall → NAT",
            ["firewall.ipv6.nat"] = "IPv6 → Firewall → NAT",
            ["firewall.ipv4.raw"] = "IP → Firewall → Raw",
            ["firewall.ipv6.raw"] = "IPv6 → Firewall → Raw",
            ["firewall.ipv4.mangle"] = "IP → Firewall → Mangle",
            ["firewall.ipv6.mangle"] = "IPv6 → Firewall → Mangle",
            ["firewall.connection-tracking"] = "IP → Firewall → Connection Tracking",
            ["ha.vrrp"] = "IP → VRRP",
            ["routing.table"] = "IP → Routes",
            ["routing.rule"] = "IP → Routing → Rules",
            ["routing.bgp"] = "Routing → BGP",
            ["routing.ospf"] = "Routing → OSPF",
            ["interface"] = "Interfaces",
            ["interface.vlan"] = "Interfaces → VLAN",
            ["interface.veth"] = "Interfaces → VETH",
            ["interface.bridge"] = "Interfaces → Bridge",
            ["bridge.instances"] = "Bridge → Bridge",
            ["container"] = "Container",
            ["ip.vrf"] = "IP → VRF",
            ["system.identity"] = "System → Identity",
            ["system.resource"] = "System → Resource",
            ["system.routerboard"] = "System → RouterBoard",
        };

    private static readonly Dictionary<string, string> PropertyLabels =
        new(StringComparer.Ordinal)
        {
            ["chain"] = "Chain",
            ["action"] = "Action",
            ["protocol"] = "Protocol",
            ["src-address"] = "Src. Address",
            ["dst-address"] = "Dst. Address",
            ["src-port"] = "Src. Port",
            ["dst-port"] = "Dst. Port",
            ["in-interface"] = "In. Interface",
            ["out-interface"] = "Out. Interface",
            ["in-interface-list"] = "In. Interface List",
            ["out-interface-list"] = "Out. Interface List",
            ["connection-state"] = "Connection State",
            ["connection-mark"] = "Connection Mark",
            ["comment"] = "Comment",
            ["disabled"] = "Disabled",
            ["log"] = "Log",
            ["log-prefix"] = "Log Prefix",
            ["tcp-flags"] = "TCP Flags",
            ["icmp-options"] = "ICMP Options",
            ["layer7-protocol"] = "Layer7 Protocol",
            ["address-list"] = "Address List",
            ["address-list-timeout"] = "Address List Timeout",
            ["hotspot"] = "Hotspot",
            ["hw-offload"] = "HW Offload",
            ["ipsec-policy"] = "IPsec Policy",
            ["p2p"] = "P2P",
            ["packet-mark"] = "Packet Mark",
            ["port"] = "Port",
            ["priority"] = "Priority",
            ["routing-mark"] = "Routing Mark",
            ["tls-host"] = "TLS Host",
            ["vlan-id"] = "VLAN ID",
            ["vrf"] = "VRF",
            ["gateway"] = "Gateway",
            ["distance"] = "Distance",
            ["routing-table"] = "Routing Table",
            ["pref-src"] = "Pref. Source",
            ["scope"] = "Scope",
            ["target-scope"] = "Target Scope",
            ["check-gateway"] = "Check Gateway",
            ["name"] = "Name",
            ["type"] = "Type",
            ["mtu"] = "MTU",
            ["mac-address"] = "MAC Address",
            ["interface"] = "Interface",
            ["vrid"] = "VRID",
            ["preemption-mode"] = "Preemption Mode",
            ["authentication"] = "Authentication",
            ["password"] = "Password",
            ["version"] = "Version",
            ["virtual-address"] = "Virtual Address",
            ["synced"] = "Synced",
            ["master"] = "Master",
            ["backup"] = "Backup",
            ["identity"] = "Identity",
            ["board-name"] = "Board Name",
            ["platform"] = "Platform",
            ["uptime"] = "Uptime",
            ["cpu-load"] = "CPU Load",
            ["free-memory"] = "Free Memory",
            ["total-memory"] = "Total Memory",
        };

    private static readonly Dictionary<string, string> MetadataLabels =
        new(StringComparer.Ordinal)
        {
            ["configuration_hash"] = "Configuration digest",
            ["operational_hash"] = "Operational digest",
            ["observation_hash"] = "Observation digest",
            ["capability_hash"] = "Capability digest",
            ["snapshot_hash"] = "Snapshot digest",
            ["content_hash"] = "Policy content digest",
            ["semantic_diff_hash"] = "Semantic diff digest",
            ["baseline"] = "Baseline digest",
            ["actual"] = "Actual digest",
            ["desired"] = "Desired digest",
            ["committed"] = "Committed digest",
            ["management_path_context_hash"] = "Management-path context digest",
            ["fasttrack_context_hash"] = "FastTrack context digest",
            ["next-hops"] = "Next hops",
            ["egress-if"] = "Egress interface",
            ["table"] = "Routing table",
            ["prefix"] = "Matched prefix",
            ["path"] = "Execution path",
            ["subject"] = "Subject",
        };
}
