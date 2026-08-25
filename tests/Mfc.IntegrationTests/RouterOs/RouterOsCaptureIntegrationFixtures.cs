using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Snapshot;

namespace Mfc.IntegrationTests.RouterOs;

internal static class RouterOsCaptureIntegrationFixtures
{
    public static RouterOsDiscoveryDataset MinimalDataset()
    {
        SystemServiceDiscoveryResult system = new()
        {
            Identity = new SystemIdentityDiscovery
            {
                Name = "chr-pilot",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Resource = new SystemResourceDiscovery
            {
                Version = "7.16.2",
                BuildTime = "2024-11-26",
                ArchitectureName = "x86_64",
                BoardName = "CHR",
                Platform = "MikroTik",
                Uptime = "1h",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Routerboard = new SystemRouterboardDiscovery
            {
                Available = false,
                Routerboard = null,
                Model = null,
                SerialNumber = null,
                FirmwareType = null,
                FactoryFirmware = null,
                CurrentFirmware = null,
                UpgradeFirmware = null,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Packages =
            [
                new SystemPackageDiscovery
                {
                    Id = "*1",
                    Name = "routeros",
                    Version = "7.16.2",
                    BuildTime = null,
                    Scheduled = null,
                    Disabled = "false",
                    RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
            Clock = new SystemClockDiscovery
            {
                Time = "12:00:00",
                Date = "2026-08-24",
                TimeZoneName = "UTC",
                GmtOffset = "+00:00",
                DstActive = "false",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            ApiSsl = new ApiSslServiceDiscovery
            {
                Found = true,
                Disabled = false,
                Port = "8729",
                AddressPrefixes = null,
                Certificate = "api-ssl",
                TlsVersion = "only-1.2",
                Vrf = null,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Warnings = [],
        };

        InterfaceAddressDiscoveryResult interfaces = InterfaceAddressDiscovery.BuildResult(
            Ok(RosReadCommandId.Interfaces, Row(("name", "ether1"), ("type", "ether"))),
            Ok(RosReadCommandId.Ipv4Addresses),
            Ok(RosReadCommandId.Ipv6Addresses),
            Ok(RosReadCommandId.InterfaceLists),
            Ok(RosReadCommandId.InterfaceListMembers));
        FirewallFilterDiscoveryResult firewall = FirewallFilterDiscovery.BuildResult(
            Ok(RosReadCommandId.Ipv4Filter),
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));
        RoutingDependencyDiscoveryResult routing = RoutingDependencyDiscovery.BuildResult(
            Ok(RosReadCommandId.RoutingTables),
            Ok(RosReadCommandId.RoutingRules),
            Ok(RosReadCommandId.Ipv4StaticRoutes),
            Ok(RosReadCommandId.Ipv6StaticRoutes),
            Ok(RosReadCommandId.Ipv4DefaultRouteState),
            Ok(RosReadCommandId.Ipv6DefaultRouteState),
            Ok(RosReadCommandId.Ipv4Nat),
            Ok(RosReadCommandId.Ipv6Nat),
            Ok(RosReadCommandId.Ipv4Raw),
            Ok(RosReadCommandId.Ipv6Raw),
            Ok(RosReadCommandId.Ipv4Mangle),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings),
            Ok(RosReadCommandId.Ipv6Settings));
        VrrpDiscoveryResult vrrp = VrrpDiscovery.BuildResult(Ok(RosReadCommandId.VrrpInterfaces), interfaces);
        BridgeSwitchDiscoveryResult bridge = BridgeSwitchDiscovery.BuildResult(
            Ok(RosReadCommandId.Bridges),
            Ok(RosReadCommandId.BridgePorts),
            Ok(RosReadCommandId.BridgeSettings),
            Ok(RosReadCommandId.BridgeVlans),
            Ok(RosReadCommandId.EthernetSwitches),
            Ok(RosReadCommandId.EthernetSwitchPorts));
        CapabilityEvaluationResult capabilities = CapabilityProfileEvaluator.Evaluate(system);
        DateTimeOffset now = new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);

        return new RouterOsDiscoveryDataset
        {
            System = system,
            Interfaces = interfaces,
            Firewall = firewall,
            Routing = routing,
            Vrrp = vrrp,
            BridgeSwitch = bridge,
            PacketPathTopology = null,
            Capabilities = capabilities,
            CommandResults = new Dictionary<RosReadCommandId, RosReadCommandResult>
            {
                [RosReadCommandId.SystemIdentity] = Ok(
                    RosReadCommandId.SystemIdentity,
                    Row(("name", "chr-pilot"))),
            },
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(1),
        };
    }

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
