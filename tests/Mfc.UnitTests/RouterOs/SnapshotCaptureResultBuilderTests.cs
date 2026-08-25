using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Snapshot;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class SnapshotCaptureResultBuilderTests
{
    [Fact]
    public void BuildProducesDeterministicHashesForFixtureDataset()
    {
        RouterOsDiscoveryDataset dataset = RouterOsCaptureTestFixtures.MinimalChrDataset();
        SnapshotCaptureResult first = SnapshotCaptureResultBuilder.Build(dataset);
        SnapshotCaptureResult second = SnapshotCaptureResultBuilder.Build(dataset);

        Assert.Equal(first.ConfigurationHash, second.ConfigurationHash);
        Assert.Equal(first.ObservationHash, second.ObservationHash);
        Assert.Equal(first.CapabilityHash, second.CapabilityHash);
        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
        Assert.NotEmpty(first.RawPayload.ToArray());
        Assert.NotEmpty(first.ConfigurationPayload.ToArray());
        Assert.NotEmpty(first.Sections);
    }

    [Fact]
    public void BuildMapsCapabilityHashFromEvaluator()
    {
        RouterOsDiscoveryDataset dataset = RouterOsCaptureTestFixtures.MinimalChrDataset();
        SnapshotCaptureResult capture = SnapshotCaptureResultBuilder.Build(dataset);
        Assert.Equal(dataset.Capabilities.CapabilityHash, capture.CapabilityHash);
    }
}

internal static class RouterOsCaptureTestFixtures
{
    public static RouterOsDiscoveryDataset MinimalChrDataset()
    {
        SystemServiceDiscoveryResult system = MinimalSystem();
        InterfaceAddressDiscoveryResult interfaces = InterfaceAddressDiscovery.BuildResult(
            Ok(RosReadCommandId.Interfaces, Row(("name", "ether1"), ("type", "ether"), ("disabled", "false"))),
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

        Dictionary<RosReadCommandId, RosReadCommandResult> commands = new()
        {
            [RosReadCommandId.SystemIdentity] = Ok(
                RosReadCommandId.SystemIdentity,
                Row(("name", system.Identity.Name ?? "chr-pilot"))),
            [RosReadCommandId.SystemResource] = Ok(
                RosReadCommandId.SystemResource,
                Row(
                    ("version", system.Resource.Version ?? "7.16.2"),
                    ("architecture-name", system.Resource.ArchitectureName ?? "x86_64"),
                    ("board-name", system.Resource.BoardName ?? "CHR"),
                    ("uptime", system.Resource.Uptime ?? "1h"))),
            [RosReadCommandId.IpServices] = Ok(
                RosReadCommandId.IpServices,
                Row(("name", "api-ssl"), ("port", "8729"), ("disabled", "false"))),
            [RosReadCommandId.Interfaces] = Ok(
                RosReadCommandId.Interfaces,
                Row(("name", "ether1"), ("type", "ether"), ("running", "true"))),
        };

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
            CommandResults = commands,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(1),
        };
    }

    private static SystemServiceDiscoveryResult MinimalSystem()
        => new()
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
