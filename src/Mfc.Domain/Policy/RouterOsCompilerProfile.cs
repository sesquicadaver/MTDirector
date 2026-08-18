using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Exact RouterOS matcher/effect tokens for compiler v1 (Compiler Spec §15 / §20).
/// Unknown tokens are compile errors, never fallbacks.
/// </summary>
public static class RouterOsCompilerProfile
{
    /// <summary>Stable profile identity token for layout v1 Controllers.</summary>
    public const string LayoutV1ProfileId = "mfc.compiler.profile.layout.v1";

    /// <summary>SHA-256 of <see cref="LayoutV1ProfileId"/> (UTF-8). Used as <c>compiler_profile_hash</c>.</summary>
    public static Hash256 LayoutV1Hash { get; } =
        Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(LayoutV1ProfileId)));

    private static readonly FrozenSet<string> SupportedMatcherKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "src-address-list",
        "dst-address-list",
        "in-interface",
        "out-interface",
        "in-interface-list",
        "out-interface-list",
        "protocol",
        "src-port",
        "dst-port",
        "icmp-options",
        "connection-state",
        "connection-nat-state",
        "src-address-type",
        "dst-address-type",
        "tcp-flags",
        "ipsec-policy",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, byte> ProtocolNumbers =
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["icmp"] = IpProtocol.Icmp,
            ["igmp"] = 2,
            ["ipip"] = 4,
            ["tcp"] = IpProtocol.Tcp,
            ["udp"] = IpProtocol.Udp,
            ["gre"] = 47,
            ["esp"] = 50,
            ["ah"] = 51,
            ["ipv6"] = 41,
            ["ipv6-icmp"] = IpProtocol.IcmpV6,
            ["icmpv6"] = IpProtocol.IcmpV6,
            ["ospf"] = 89,
            ["pim"] = 103,
            ["vrrp"] = IpProtocol.Vrrp,
            ["l2tp"] = 115,
            ["sctp"] = IpProtocol.Sctp,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedMatcherKey(string key)
        => !string.IsNullOrWhiteSpace(key) && SupportedMatcherKeys.Contains(key.Trim());

    /// <summary>
    /// Validates a candidate matcher and rewrites <c>protocol</c> names to numeric IDs.
    /// Unknown keys or tokens fail closed with <see cref="PolicyCompilerCodes.UnsupportedMatcher"/>.
    /// </summary>
    public static bool TryNormalizeMatcher(
        string key,
        string value,
        out string normalizedKey,
        out string normalizedValue,
        out string? errorCode)
    {
        normalizedKey = string.Empty;
        normalizedValue = string.Empty;
        errorCode = null;
        if (string.IsNullOrWhiteSpace(key) || value is null)
        {
            errorCode = PolicyCompilerCodes.UnsupportedMatcher;
            return false;
        }

        string trimmedKey = key.Trim();
        if (!SupportedMatcherKeys.Contains(trimmedKey))
        {
            errorCode = PolicyCompilerCodes.UnsupportedMatcher;
            return false;
        }

        if (value.Trim().Length == 0)
        {
            errorCode = PolicyCompilerCodes.UnsupportedMatcher;
            return false;
        }

        string trimmedValue = value.Trim();
        if (trimmedKey == "protocol")
        {
            if (!TryNormalizeProtocol(trimmedValue, out string numeric, out errorCode))
            {
                return false;
            }

            normalizedKey = trimmedKey;
            normalizedValue = numeric;
            return true;
        }

        if (trimmedKey == "icmp-options" && !IsIcmpOptionsToken(trimmedValue))
        {
            errorCode = PolicyCompilerCodes.UnsupportedMatcher;
            return false;
        }

        normalizedKey = trimmedKey;
        normalizedValue = trimmedValue;
        return true;
    }

    public static bool TryNormalizeProtocol(string raw, out string numeric, out string? errorCode)
    {
        numeric = string.Empty;
        errorCode = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            errorCode = PolicyCompilerCodes.UnsupportedMatcher;
            return false;
        }

        string token = raw.Trim();
        if (byte.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out byte number))
        {
            numeric = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (ProtocolNumbers.TryGetValue(token, out byte mapped))
        {
            numeric = mapped.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        errorCode = PolicyCompilerCodes.UnsupportedMatcher;
        return false;
    }

    public static bool TryFormatConnectionStates(
        IReadOnlyList<ConnectionState> states,
        out string token,
        out string? errorCode)
        => TryFormatEnumSet(states, FormatConnectionState, out token, out errorCode);

    public static bool TryFormatConnectionNatStates(
        IReadOnlyList<ConnectionNatState> states,
        out string token,
        out string? errorCode)
        => TryFormatEnumSet(states, FormatConnectionNatState, out token, out errorCode);

    public static bool TryFormatAddressTypes(
        IReadOnlyList<AddressType> types,
        out string token,
        out string? errorCode)
        => TryFormatEnumSet(types, FormatAddressType, out token, out errorCode);

    public static bool TryFormatTcpFlags(TcpFlagConstraint flags, out string token, out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(flags);
        token = string.Empty;
        errorCode = null;
        List<string> parts = new(flags.RequiredPresent.Count + flags.RequiredAbsent.Count);
        foreach (TcpHeaderBit bit in flags.RequiredPresent)
        {
            if (!TryFormatTcpFlag(bit, negated: false, out string part, out errorCode))
            {
                return false;
            }

            parts.Add(part);
        }

        foreach (TcpHeaderBit bit in flags.RequiredAbsent)
        {
            if (!TryFormatTcpFlag(bit, negated: true, out string part, out errorCode))
            {
                return false;
            }

            parts.Add(part);
        }

        token = string.Join(',', parts);
        return true;
    }

    public static bool TryFormatIpsecPolicy(IpsecPolicyPredicate predicate, out string token, out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        token = string.Empty;
        errorCode = null;
        if (!TryFormatIpsecDirection(predicate.Direction, out string direction, out errorCode)
            || !TryFormatIpsecKind(predicate.Policy, out string policy, out errorCode))
        {
            return false;
        }

        token = direction + "," + policy;
        return true;
    }

    public static bool TryFormatRejectWith(RejectMode mode, out string token, out string? errorCode)
    {
        token = string.Empty;
        errorCode = null;
        switch (mode)
        {
            case RejectMode.TcpReset:
                token = "tcp-reset";
                return true;
            case RejectMode.AdminProhibited:
                token = "icmp-admin-prohibited";
                return true;
            case RejectMode.PortUnreachable:
                token = "icmp-port-unreachable";
                return true;
            default:
                errorCode = PolicyCompilerCodes.RejectModeUnsupported;
                return false;
        }
    }

    /// <summary>Infallible mapping for already-validated reject modes (layout terminals).</summary>
    public static string FormatRejectWith(RejectMode mode)
    {
        if (TryFormatRejectWith(mode, out string token, out _))
        {
            return token;
        }

        throw new DomainInvariantException($"Unsupported reject_mode '{mode}'.");
    }

    private static bool TryFormatEnumSet<T>(
        IReadOnlyList<T> values,
        Func<T, string?> formatter,
        out string token,
        out string? errorCode)
        where T : struct, Enum
    {
        token = string.Empty;
        errorCode = null;
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return true;
        }

        StringBuilder builder = new();
        foreach (T value in values.OrderBy(static v => Convert.ToByte(v, CultureInfo.InvariantCulture)))
        {
            string? part = formatter(value);
            if (part is null)
            {
                errorCode = PolicyCompilerCodes.UnsupportedMatcher;
                return false;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(part);
        }

        token = builder.ToString();
        return true;
    }

    private static string? FormatConnectionState(ConnectionState state)
        => state switch
        {
            ConnectionState.New => "new",
            ConnectionState.Established => "established",
            ConnectionState.Related => "related",
            ConnectionState.Invalid => "invalid",
            ConnectionState.Untracked => "untracked",
            _ => null,
        };

    private static string? FormatConnectionNatState(ConnectionNatState state)
        => state switch
        {
            ConnectionNatState.SrcNat => "srcnat",
            ConnectionNatState.DstNat => "dstnat",
            _ => null,
        };

    private static string? FormatAddressType(AddressType type)
        => type switch
        {
            AddressType.Local => "local",
            AddressType.Unicast => "unicast",
            AddressType.Broadcast => "broadcast",
            AddressType.Multicast => "multicast",
            AddressType.Anycast => "anycast",
            AddressType.Blackhole => "blackhole",
            AddressType.Prohibit => "prohibit",
            AddressType.Unreachable => "unreachable",
            _ => null,
        };

    private static bool TryFormatTcpFlag(TcpHeaderBit bit, bool negated, out string token, out string? errorCode)
    {
        token = string.Empty;
        errorCode = null;
        string? name = bit switch
        {
            TcpHeaderBit.Fin => "fin",
            TcpHeaderBit.Syn => "syn",
            TcpHeaderBit.Rst => "rst",
            TcpHeaderBit.Psh => "psh",
            TcpHeaderBit.Ack => "ack",
            TcpHeaderBit.Urg => "urg",
            TcpHeaderBit.Ece => "ece",
            TcpHeaderBit.Cwr => "cwr",
            _ => null,
        };
        if (name is null)
        {
            errorCode = PolicyCompilerCodes.UnsupportedMatcher;
            return false;
        }

        token = negated ? "!" + name : name;
        return true;
    }

    private static bool TryFormatIpsecDirection(IpsecDirection direction, out string token, out string? errorCode)
    {
        token = string.Empty;
        errorCode = null;
        switch (direction)
        {
            case IpsecDirection.In:
                token = "in";
                return true;
            case IpsecDirection.Out:
                token = "out";
                return true;
            default:
                errorCode = PolicyCompilerCodes.UnsupportedMatcher;
                return false;
        }
    }

    private static bool TryFormatIpsecKind(IpsecPolicyKind kind, out string token, out string? errorCode)
    {
        token = string.Empty;
        errorCode = null;
        switch (kind)
        {
            case IpsecPolicyKind.Ipsec:
                token = "ipsec";
                return true;
            case IpsecPolicyKind.None:
                token = "none";
                return true;
            default:
                errorCode = PolicyCompilerCodes.UnsupportedMatcher;
                return false;
        }
    }

    private static bool IsIcmpOptionsToken(string value)
    {
        string trimmed = value.Trim();
        int colon = trimmed.IndexOf(':');
        if (colon < 0)
        {
            return byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }

        if (colon == 0 || colon == trimmed.Length - 1 || trimmed.IndexOf(':', colon + 1) >= 0)
        {
            return false;
        }

        return byte.TryParse(trimmed[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out _)
               && byte.TryParse(trimmed[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }
}
