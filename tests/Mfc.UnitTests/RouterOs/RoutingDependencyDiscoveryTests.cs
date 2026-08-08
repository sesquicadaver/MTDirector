using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class RoutingDependencyDiscoveryTests
{
    [Fact]
    public void SeparatesStaticConfigFromActiveRouteObservationsAndTypesMetrics()
    {
        RoutingDependencyDiscoveryResult result = FirewallFilterDiscoveryLike();

        Assert.Single(result.Ipv4StaticRoutes);
        StaticRouteDiscovery route = result.Ipv4StaticRoutes[0];
        Assert.Equal(1, route.Distance);
        Assert.Equal(30, route.Scope);
        Assert.Equal(10, route.TargetScope);
        Assert.False(route.IsDynamic);
        Assert.Equal("true", route.Active);
        Assert.Equal("reachable", route.GatewayStatus);

        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.Contains("gateway-status", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Values, v => v == "reachable");
        Assert.Contains(result.ConfigurationHashMaterial.Keys, k => k.Contains("distance", StringComparison.Ordinal));

        Assert.Single(result.Ipv4DefaultRouteState);
        Assert.Equal("1.1.1.1", result.Ipv4DefaultRouteState[0].Gateway);
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.StartsWith("default", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatesRoutingTableReferencesAndKeepsFacilityOrder()
    {
        RoutingDependencyDiscoveryResult result = FirewallFilterDiscoveryLike(missingTable: true);

        Assert.Contains(
            result.Findings,
            f => f.Code == DiscoveryFinding.MissingRoutingTableReference && f.Subject == "wan2");
        Assert.Equal(2, result.Ipv4NatRules.Count);
        Assert.Equal(0, result.Ipv4NatRules[0].EffectiveOrdinal);
        Assert.Equal(1, result.Ipv4NatRules[1].EffectiveOrdinal);
        Assert.Equal("srcnat", result.Ipv4NatRules[0].Chain);
        Assert.Equal("dstnat", result.Ipv4NatRules[1].Chain);
        Assert.Equal(2, result.Ipv4MangleRules.Count);
        Assert.Equal("mark-routing", result.Ipv4MangleRules[0].Action);
    }

    [Fact]
    public void MarksPccNthRandomAsUnsupportedForEditing()
    {
        RosReadCommandResult mangle = Ok(
            RosReadCommandId.Ipv4Mangle,
            Row(
                ("chain", "prerouting"),
                ("action", "mark-routing"),
                ("per-connection-classifier", "both-addresses:2/0"),
                ("nth", "2,1"),
                ("new-routing-mark", "wan1")));

        RoutingDependencyDiscoveryResult result = BuildMinimal(ipv4Mangle: mangle);
        OrderedFirewallFacilityRuleDiscovery rule = Assert.Single(result.Ipv4MangleRules);
        Assert.True(rule.UnsupportedForEditing);
        Assert.Contains("per-connection-classifier", rule.UnsupportedMatchers);
        Assert.Contains("nth", rule.UnsupportedMatchers);
        Assert.Contains(
            result.Findings,
            f => f.Code == DiscoveryFinding.UnsupportedForEditing);
        Assert.Equal("true", result.ConfigurationHashMaterial["mangle4.0.unsupported"]);
    }

    [Fact]
    public void ExposesRpFilterForTopologyValidator()
    {
        RoutingDependencyDiscoveryResult result = BuildMinimal(
            ipv4Settings: Ok(
                RosReadCommandId.Ipv4Settings,
                Row(("rp-filter", "strict"), ("ip-forward", "true"))));

        Assert.Equal("strict", result.Ipv4Settings.RpFilter);
        Assert.Equal("strict", result.ConfigurationHashMaterial["ip4.rp-filter"]);
    }

    [Fact]
    public void DiscoveryCommandSetNeverTouchesVpnPeerSecrets()
    {
        foreach (RosReadCommandId id in RoutingDependencyDiscovery.DiscoveryCommandIds)
        {
            string path = RosReadCommandRegistry.Get(id).FixedPath;
            Assert.DoesNotContain("/ppp/", path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/ipsec/", path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("/print", path, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DynamicRoutesAreNotMixedIntoStaticConfiguration()
    {
        RosReadCommandResult routes = Ok(
            RosReadCommandId.Ipv4StaticRoutes,
            Row(
                ("dst-address", "10.0.0.0/8"),
                ("gateway", "192.168.0.1"),
                ("distance", "1"),
                ("scope", "30"),
                ("target-scope", "10"),
                ("static", "true"),
                ("dynamic", "false"),
                ("routing-table", "main")),
            Row(
                ("dst-address", "0.0.0.0/0"),
                ("gateway", "9.9.9.9"),
                ("distance", "1"),
                ("dynamic", "true"),
                ("static", "false"),
                ("routing-table", "main")));

        RoutingDependencyDiscoveryResult result = BuildMinimal(ipv4StaticRoutes: routes);
        Assert.Single(result.Ipv4StaticRoutes);
        Assert.Equal("10.0.0.0/8", result.Ipv4StaticRoutes[0].DstAddress);
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Values, v => v == "9.9.9.9");
    }

    private static RoutingDependencyDiscoveryResult FirewallFilterDiscoveryLike(bool missingTable = false)
    {
        string table = missingTable ? "wan2" : "main";
        return BuildMinimal(
            routingTables: Ok(
                RosReadCommandId.RoutingTables,
                Row(("name", "main"), ("fib", "yes"))),
            routingRules: Ok(
                RosReadCommandId.RoutingRules,
                Row(("action", "lookup"), ("table", table), ("routing-mark", "wan2"))),
            ipv4StaticRoutes: Ok(
                RosReadCommandId.Ipv4StaticRoutes,
                Row(
                    ("dst-address", "0.0.0.0/0"),
                    ("gateway", "1.1.1.1"),
                    ("distance", "1"),
                    ("scope", "30"),
                    ("target-scope", "10"),
                    ("routing-table", "main"),
                    ("static", "true"),
                    ("dynamic", "false"),
                    ("active", "true"),
                    ("gateway-status", "reachable"),
                    ("immediate-gw", "1.1.1.1%ether1"))),
            ipv4DefaultRouteState: Ok(
                RosReadCommandId.Ipv4DefaultRouteState,
                Row(
                    ("dst-address", "0.0.0.0/0"),
                    ("gateway", "1.1.1.1"),
                    ("distance", "1"),
                    ("active", "true"),
                    ("gateway-status", "reachable"),
                    ("immediate-gw", "1.1.1.1%ether1"),
                    ("static", "true"))),
            ipv4Nat: Ok(
                RosReadCommandId.Ipv4Nat,
                Row(("chain", "srcnat"), ("action", "masquerade")),
                Row(("chain", "dstnat"), ("action", "dst-nat"), ("to-addresses", "10.0.0.2"))),
            ipv4Mangle: Ok(
                RosReadCommandId.Ipv4Mangle,
                Row(("chain", "prerouting"), ("action", "mark-routing"), ("new-routing-mark", "wan1")),
                Row(("chain", "prerouting"), ("action", "mark-connection"), ("new-connection-mark", "c1"))),
            ipv4Settings: Ok(
                RosReadCommandId.Ipv4Settings,
                Row(("rp-filter", "loose"), ("ip-forward", "true"))));
    }

    private static RoutingDependencyDiscoveryResult BuildMinimal(
        RosReadCommandResult? routingTables = null,
        RosReadCommandResult? routingRules = null,
        RosReadCommandResult? ipv4StaticRoutes = null,
        RosReadCommandResult? ipv6StaticRoutes = null,
        RosReadCommandResult? ipv4DefaultRouteState = null,
        RosReadCommandResult? ipv6DefaultRouteState = null,
        RosReadCommandResult? ipv4Nat = null,
        RosReadCommandResult? ipv6Nat = null,
        RosReadCommandResult? ipv4Raw = null,
        RosReadCommandResult? ipv6Raw = null,
        RosReadCommandResult? ipv4Mangle = null,
        RosReadCommandResult? ipv6Mangle = null,
        RosReadCommandResult? ipv4Settings = null,
        RosReadCommandResult? ipv6Settings = null)
        => RoutingDependencyDiscovery.BuildResult(
            routingTables ?? Ok(RosReadCommandId.RoutingTables, Row(("name", "main"), ("fib", "yes"))),
            routingRules ?? Ok(RosReadCommandId.RoutingRules),
            ipv4StaticRoutes ?? Ok(RosReadCommandId.Ipv4StaticRoutes),
            ipv6StaticRoutes ?? Ok(RosReadCommandId.Ipv6StaticRoutes),
            ipv4DefaultRouteState ?? Ok(RosReadCommandId.Ipv4DefaultRouteState),
            ipv6DefaultRouteState ?? Ok(RosReadCommandId.Ipv6DefaultRouteState),
            ipv4Nat ?? Ok(RosReadCommandId.Ipv4Nat),
            ipv6Nat ?? Ok(RosReadCommandId.Ipv6Nat),
            ipv4Raw ?? Ok(RosReadCommandId.Ipv4Raw),
            ipv6Raw ?? Ok(RosReadCommandId.Ipv6Raw),
            ipv4Mangle ?? Ok(RosReadCommandId.Ipv4Mangle),
            ipv6Mangle ?? Ok(RosReadCommandId.Ipv6Mangle),
            ipv4Settings ?? Ok(RosReadCommandId.Ipv4Settings, Row(("rp-filter", "no"))),
            ipv6Settings ?? Ok(RosReadCommandId.Ipv6Settings, Row(("forward", "true"))));

    private static RosReadCommandResult Ok(RosReadCommandId id, params RosReadRecord[] rows)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
