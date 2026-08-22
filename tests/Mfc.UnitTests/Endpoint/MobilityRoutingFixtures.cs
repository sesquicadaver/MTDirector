using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.UnitTests.Endpoint;

internal static class MobilityRoutingFixtures
{
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    public static EndpointMobilityProbeTargets ProbeTargets()
        => new()
        {
            CorporateDestination = "10.20.0.10",
            InternetDestination = "203.0.113.10",
            WazuhDestination = "10.50.0.5",
        };

    public static RoutingAssuranceState RoutingState(DeviceId deviceId)
        => RoutingAssuranceState.Create(
            deviceId,
            Configuration(),
            Operational(),
            T10);

    public static RoutingConfigurationSnapshot Configuration()
        => new(
            [Table("main"), Table("corp")],
            new RoutingSettingsFact
            {
                PolicyRules = "lookup",
                CheckGatewayPingCount = null,
                CheckGatewayPingInterval = null,
                CheckGatewayPingTimeout = null,
                ConnectedInChain = null,
                DynamicInChain = null,
                SingleProcess = "yes",
            },
            [
                Rule(0, RoutingRuleActions.Lookup, dst: "10.0.0.0/8", table: "corp"),
            ],
            [],
            [
                Route("10.0.0.0/8", "10.99.0.1", "corp"),
                Route("0.0.0.0/0", "1.1.1.1", "main"),
            ],
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    public static RoutingOperationalSnapshot Operational()
        => new(
            [
                Obs("10.0.0.0/8", "10.99.0.1", "corp", immediateGw: "10.99.0.1%ipsec1"),
                Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
            ],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static RoutingRuleFact Rule(int ordinal, string action, string? dst = null, string? table = null)
        => new()
        {
            EffectiveOrdinal = ordinal,
            Action = action,
            SrcAddress = null,
            DstAddress = dst,
            RoutingMark = null,
            Table = table,
            Disabled = "false",
        };

    private static StaticRouteConfigFact Route(string dst, string gateway, string table)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            Gateway = gateway,
            RoutingTable = table,
            Distance = 1,
            Scope = null,
            TargetScope = null,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
        };

    private static RouteObservationFact Obs(string dst, string gateway, string table, string immediateGw)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = "true",
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = false,
            HwOffloaded = null,
        };
}
