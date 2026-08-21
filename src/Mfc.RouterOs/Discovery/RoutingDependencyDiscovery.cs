using System.Globalization;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Reads routing tables/rules/routes plus NAT/RAW/Mangle and IP settings for multi-WAN analysis (M1-14).
/// Read-only: never compiles or mutates these facilities. VPN peer credentials are never requested.
/// M7.1-01 registers <see cref="RoutingAssuranceAllowlist"/> paths (settings, VRF, filter rules);
/// discovery mapping of those new sections is deferred to M7.1-02 (RoutingAssuranceState persistence).
/// Tables, rules, and static/default routes remain fetched here as before.
/// </summary>
public static class RoutingDependencyDiscovery
{
    private static readonly string[] UnsupportedMatchers =
    [
        "per-connection-classifier",
        "nth",
        "random",
    ];

    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.RoutingTables,
        RosReadCommandId.RoutingRules,
        RosReadCommandId.Ipv4StaticRoutes,
        RosReadCommandId.Ipv6StaticRoutes,
        RosReadCommandId.Ipv4DefaultRouteState,
        RosReadCommandId.Ipv6DefaultRouteState,
        RosReadCommandId.Ipv4Nat,
        RosReadCommandId.Ipv6Nat,
        RosReadCommandId.Ipv4Raw,
        RosReadCommandId.Ipv6Raw,
        RosReadCommandId.Ipv4Mangle,
        RosReadCommandId.Ipv6Mangle,
        RosReadCommandId.Ipv4Settings,
        RosReadCommandId.Ipv6Settings,
    ];

    public static async Task<RoutingDependencyDiscoveryResult> DiscoverAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        List<string> warnings = [];
        Dictionary<RosReadCommandId, RosReadCommandResult> results = new();
        foreach (RosReadCommandId id in CommandSet)
        {
            results[id] = await ExecuteAsync(session, id, warnings, cancellationToken).ConfigureAwait(false);
        }

        return BuildResult(
            results[RosReadCommandId.RoutingTables],
            results[RosReadCommandId.RoutingRules],
            results[RosReadCommandId.Ipv4StaticRoutes],
            results[RosReadCommandId.Ipv6StaticRoutes],
            results[RosReadCommandId.Ipv4DefaultRouteState],
            results[RosReadCommandId.Ipv6DefaultRouteState],
            results[RosReadCommandId.Ipv4Nat],
            results[RosReadCommandId.Ipv6Nat],
            results[RosReadCommandId.Ipv4Raw],
            results[RosReadCommandId.Ipv6Raw],
            results[RosReadCommandId.Ipv4Mangle],
            results[RosReadCommandId.Ipv6Mangle],
            results[RosReadCommandId.Ipv4Settings],
            results[RosReadCommandId.Ipv6Settings],
            warnings);
    }

    public static RoutingDependencyDiscoveryResult BuildResult(
        RosReadCommandResult routingTables,
        RosReadCommandResult routingRules,
        RosReadCommandResult ipv4StaticRoutes,
        RosReadCommandResult ipv6StaticRoutes,
        RosReadCommandResult ipv4DefaultRouteState,
        RosReadCommandResult ipv6DefaultRouteState,
        RosReadCommandResult ipv4Nat,
        RosReadCommandResult ipv6Nat,
        RosReadCommandResult ipv4Raw,
        RosReadCommandResult ipv6Raw,
        RosReadCommandResult ipv4Mangle,
        RosReadCommandResult ipv6Mangle,
        RosReadCommandResult ipv4Settings,
        RosReadCommandResult ipv6Settings,
        IReadOnlyList<string>? warnings = null)
    {
        List<DiscoveryFinding> findings = [];
        List<RoutingTableDiscovery> tables = MapTables(routingTables);
        HashSet<string> knownTables = tables
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        // RouterOS implicit main table.
        knownTables.Add("main");

        List<RoutingRuleDiscovery> rules = MapRules(routingRules, knownTables, findings);
        List<StaticRouteDiscovery> v4Routes = MapStaticRoutes(ipv4StaticRoutes, IpAddressFamilyKind.Ipv4, knownTables, findings);
        List<StaticRouteDiscovery> v6Routes = MapStaticRoutes(ipv6StaticRoutes, IpAddressFamilyKind.Ipv6, knownTables, findings);
        List<DefaultRouteStateDiscovery> v4Defaults = MapDefaultRoutes(ipv4DefaultRouteState, IpAddressFamilyKind.Ipv4);
        List<DefaultRouteStateDiscovery> v6Defaults = MapDefaultRoutes(ipv6DefaultRouteState, IpAddressFamilyKind.Ipv6);

        return new RoutingDependencyDiscoveryResult
        {
            RoutingTables = tables.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray(),
            RoutingRules = rules,
            Ipv4StaticRoutes = v4Routes.Where(r => !r.IsDynamic).OrderBy(r => r.DstAddress, StringComparer.Ordinal).ToArray(),
            Ipv6StaticRoutes = v6Routes.Where(r => !r.IsDynamic).OrderBy(r => r.DstAddress, StringComparer.Ordinal).ToArray(),
            Ipv4DefaultRouteState = v4Defaults,
            Ipv6DefaultRouteState = v6Defaults,
            Ipv4NatRules = MapFacility(ipv4Nat, OrderedFirewallFacility.Nat, IpAddressFamilyKind.Ipv4, findings),
            Ipv6NatRules = MapFacility(ipv6Nat, OrderedFirewallFacility.Nat, IpAddressFamilyKind.Ipv6, findings),
            Ipv4RawRules = MapFacility(ipv4Raw, OrderedFirewallFacility.Raw, IpAddressFamilyKind.Ipv4, findings),
            Ipv6RawRules = MapFacility(ipv6Raw, OrderedFirewallFacility.Raw, IpAddressFamilyKind.Ipv6, findings),
            Ipv4MangleRules = MapFacility(ipv4Mangle, OrderedFirewallFacility.Mangle, IpAddressFamilyKind.Ipv4, findings),
            Ipv6MangleRules = MapFacility(ipv6Mangle, OrderedFirewallFacility.Mangle, IpAddressFamilyKind.Ipv6, findings),
            Ipv4Settings = MapIpv4Settings(ipv4Settings),
            Ipv6Settings = MapIpv6Settings(ipv6Settings),
            Findings = findings,
            Warnings = warnings?.ToArray() ?? [],
        };
    }

    /// <summary>Command allowlist used by this discovery — must never include VPN peer secret paths.</summary>
    public static IReadOnlyList<RosReadCommandId> DiscoveryCommandIds => CommandSet;

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

    private static List<RoutingTableDiscovery> MapTables(RosReadCommandResult result)
    {
        List<RoutingTableDiscovery> items = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            items.Add(new RoutingTableDiscovery
            {
                Name = Get(row, "name"),
                Fib = Get(row, "fib"),
                Disabled = Get(row, "disabled"),
                Dynamic = Get(row, "dynamic"),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static List<RoutingRuleDiscovery> MapRules(
        RosReadCommandResult result,
        HashSet<string> knownTables,
        List<DiscoveryFinding> findings)
    {
        List<RoutingRuleDiscovery> items = new(result.Records.Count);
        for (int i = 0; i < result.Records.Count; i++)
        {
            RosReadRecord row = result.Records[i];
            string? table = Get(row, "table");
            ValidateTableReference(table, knownTables, findings, "routing-rule");
            items.Add(new RoutingRuleDiscovery
            {
                EffectiveOrdinal = i,
                Action = Get(row, "action"),
                SrcAddress = Get(row, "src-address"),
                DstAddress = Get(row, "dst-address"),
                RoutingMark = Get(row, "routing-mark"),
                Table = table,
                Disabled = Get(row, "disabled"),
                Comment = Get(row, "comment"),
                IsDynamic = IsTruthy(Get(row, "dynamic")),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static List<StaticRouteDiscovery> MapStaticRoutes(
        RosReadCommandResult result,
        IpAddressFamilyKind family,
        HashSet<string> knownTables,
        List<DiscoveryFinding> findings)
    {
        List<StaticRouteDiscovery> items = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            bool isDynamic = IsTruthy(Get(row, "dynamic")) && !IsTruthy(Get(row, "static"));
            string? table = Get(row, "routing-table") ?? "main";
            if (!isDynamic)
            {
                ValidateTableReference(table, knownTables, findings, "static-route");
            }

            items.Add(new StaticRouteDiscovery
            {
                Family = family,
                DstAddress = Get(row, "dst-address"),
                Gateway = Get(row, "gateway"),
                RoutingTable = table,
                Distance = ParseInt(Get(row, "distance")),
                Scope = ParseInt(Get(row, "scope")),
                TargetScope = ParseInt(Get(row, "target-scope")),
                PrefSrc = Get(row, "pref-src"),
                CheckGateway = Get(row, "check-gateway"),
                Disabled = Get(row, "disabled"),
                Comment = Get(row, "comment"),
                IsDynamic = isDynamic,
                Active = Get(row, "active"),
                ImmediateGateway = Get(row, "immediate-gw"),
                GatewayStatus = Get(row, "gateway-status"),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static List<DefaultRouteStateDiscovery> MapDefaultRoutes(
        RosReadCommandResult result,
        IpAddressFamilyKind family)
    {
        List<DefaultRouteStateDiscovery> items = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            items.Add(new DefaultRouteStateDiscovery
            {
                Family = family,
                DstAddress = Get(row, "dst-address"),
                RoutingTable = Get(row, "routing-table") ?? "main",
                Gateway = Get(row, "gateway"),
                Distance = ParseInt(Get(row, "distance")),
                Active = Get(row, "active"),
                ImmediateGateway = Get(row, "immediate-gw"),
                GatewayStatus = Get(row, "gateway-status"),
                IsDynamic = IsTruthy(Get(row, "dynamic")),
                IsStatic = IsTruthy(Get(row, "static")),
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static List<OrderedFirewallFacilityRuleDiscovery> MapFacility(
        RosReadCommandResult result,
        OrderedFirewallFacility facility,
        IpAddressFamilyKind family,
        List<DiscoveryFinding> findings)
    {
        List<OrderedFirewallFacilityRuleDiscovery> items = new(result.Records.Count);
        for (int i = 0; i < result.Records.Count; i++)
        {
            RosReadRecord row = result.Records[i];
            List<string> unsupported = [];
            foreach (string matcher in UnsupportedMatchers)
            {
                if (row.KnownProperties.TryGetValue(matcher, out string? value)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    unsupported.Add(matcher);
                }
            }

            if (unsupported.Count > 0)
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.UnsupportedForEditing,
                    Message = $"{facility}/{family} rule@{i} uses unsupported-for-editing matchers: {string.Join(',', unsupported)}.",
                    Subject = $"{facility}:{i}",
                });
            }

            items.Add(new OrderedFirewallFacilityRuleDiscovery
            {
                Facility = facility,
                Family = family,
                EffectiveOrdinal = i,
                Chain = Get(row, "chain"),
                Action = Get(row, "action"),
                Disabled = Get(row, "disabled"),
                Comment = Get(row, "comment"),
                ConnectionMark = Get(row, "connection-mark"),
                PacketMark = Get(row, "packet-mark"),
                RoutingMark = Get(row, "routing-mark"),
                NewRoutingMark = Get(row, "new-routing-mark"),
                UnsupportedForEditing = unsupported.Count > 0,
                UnsupportedMatchers = unsupported,
                KnownProperties = row.KnownProperties,
                RawProperties = row.RawProperties,
            });
        }

        return items;
    }

    private static Ipv4SettingsDiscovery MapIpv4Settings(RosReadCommandResult result)
    {
        RosReadRecord row = FirstOrEmpty(result);
        return new Ipv4SettingsDiscovery
        {
            IpForward = Get(row, "ip-forward"),
            RpFilter = Get(row, "rp-filter"),
            AcceptSourceRoute = Get(row, "accept-source-route"),
            AllowFastPath = Get(row, "allow-fast-path"),
            TcpSyncookies = Get(row, "tcp-syncookies"),
            Ipv4FasttrackActive = Get(row, "ipv4-fasttrack-active"),
            RawProperties = row.RawProperties,
        };
    }

    private static Ipv6SettingsDiscovery MapIpv6Settings(RosReadCommandResult result)
    {
        RosReadRecord row = FirstOrEmpty(result);
        return new Ipv6SettingsDiscovery
        {
            Forward = Get(row, "forward"),
            DisableIpv6 = Get(row, "disable-ipv6"),
            AcceptRouterAdvertisements = Get(row, "accept-router-advertisements"),
            RawProperties = row.RawProperties,
        };
    }

    private static void ValidateTableReference(
        string? table,
        HashSet<string> knownTables,
        List<DiscoveryFinding> findings,
        string context)
    {
        if (string.IsNullOrWhiteSpace(table) || knownTables.Contains(table))
        {
            return;
        }

        findings.Add(new DiscoveryFinding
        {
            Code = DiscoveryFinding.MissingRoutingTableReference,
            Message = $"{context} references unknown routing table '{table}'.",
            Subject = table,
        });
    }

    private static RosReadRecord FirstOrEmpty(RosReadCommandResult result)
    {
        if (result.Records.Count > 0)
        {
            return result.Records[0];
        }

        return new RosReadRecord
        {
            KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? Get(RosReadRecord row, string name)
        => row.KnownProperties.TryGetValue(name, out string? value) ? value : null;
}
