using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Living Spec matrix for Issue Set M7.1-09 AC (routing configuration vs operational drift).</summary>
public sealed class RoutingDriftLivingSpecTests
{
    [Fact]
    public void Ac1RoutingTableFibChangeIsConfigurationDrift()
    {
        RoutingConfigurationSnapshot previous = Config(["rtab.main.fib"], "yes");
        RoutingConfigurationSnapshot current = Config(["rtab.main.fib"], "no");

        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            previous,
            Ops([]),
            current,
            Ops([]));

        Assert.True(result.IsConfigurationDrift);
        Assert.False(result.IsOperationalChange);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.RoutingTableChanged);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.ConfigurationDrift);
        Assert.DoesNotContain(result.Findings, f => f.Code == RoutingDriftCodes.OperationalChange);
    }

    [Fact]
    public void Ac2ActiveAndGatewayStatusChangeOnlyIsOperationalNotConfigurationDrift()
    {
        Dictionary<string, string> previousOps = new(StringComparer.Ordinal)
        {
            ["route.4:main:0.0.0.0/0:1.1.1.1.active"] = "true",
            ["route.4:main:0.0.0.0/0:1.1.1.1.gateway-status"] = "reachable",
        };
        Dictionary<string, string> currentOps = new(StringComparer.Ordinal)
        {
            ["route.4:main:0.0.0.0/0:1.1.1.1.active"] = "false",
            ["route.4:main:0.0.0.0/0:1.1.1.1.gateway-status"] = "unreachable",
        };

        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            Config([]),
            OpsMaterial(previousOps),
            Config([]),
            OpsMaterial(currentOps));

        Assert.False(result.IsConfigurationDrift);
        Assert.True(result.IsOperationalChange);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.ActiveRouteChanged);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.GatewayUnreachable);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.OperationalChange);
        Assert.DoesNotContain(result.Findings, f => f.Code == RoutingDriftCodes.ConfigurationDrift);
    }

    [Fact]
    public void Ac3StaticRouteDistanceScopeChangeIsConfigurationDrift()
    {
        RoutingConfigurationSnapshot previous = Config(
        [
            ("route.4:main:10.0.0.0/8:10.1.1.1.distance", "1"),
            ("route.4:main:10.0.0.0/8:10.1.1.1.scope", "30"),
        ]);
        RoutingConfigurationSnapshot current = Config(
        [
            ("route.4:main:10.0.0.0/8:10.1.1.1.distance", "5"),
            ("route.4:main:10.0.0.0/8:10.1.1.1.scope", "10"),
        ]);

        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            previous,
            Ops([]),
            current,
            Ops([]));

        Assert.True(result.IsConfigurationDrift);
        Assert.False(result.IsOperationalChange);
        Assert.All(
            result.Findings.Where(f => f.Code == RoutingDriftCodes.StaticRouteChanged),
            f => Assert.Contains("route.4:main:10.0.0.0/8:10.1.1.1", f.Subject!, StringComparison.Ordinal));
    }

    [Fact]
    public void Ac4DefaultRouteGatewayChangeIsOperationalDefaultWanChanged()
    {
        Dictionary<string, string> previousOps = new(StringComparer.Ordinal)
        {
            ["default.4:main:1.1.1.1.active"] = "true",
        };
        Dictionary<string, string> currentOps = new(StringComparer.Ordinal)
        {
            ["default.4:main:2.2.2.2.active"] = "true",
        };

        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            Config([]),
            OpsMaterial(previousOps),
            Config([]),
            OpsMaterial(currentOps));

        Assert.False(result.IsConfigurationDrift);
        Assert.True(result.IsOperationalChange);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.DefaultWanChanged);
    }

    [Fact]
    public void Ac5ConfigHashUnchangedOpsHashChangedIsOperationalOnly()
    {
        RoutingConfigurationSnapshot configuration = Config(["rtab.main.fib"], "yes");
        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            configuration,
            Ops(["route.4:main:0.0.0.0/0:1.1.1.1.active"], "true"),
            configuration,
            Ops(["route.4:main:0.0.0.0/0:1.1.1.1.active"], "false"));

        Assert.False(result.ConfigurationHashChanged);
        Assert.True(result.OperationalHashChanged);
        Assert.False(result.IsConfigurationDrift);
        Assert.True(result.IsOperationalChange);
    }

    [Fact]
    public void Ac6ConfigHashChangedIsConfigurationDriftEvenWhenOpsAlsoChanged()
    {
        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            Config(["rtab.main.fib"], "yes"),
            Ops(["route.4:main:0.0.0.0/0:1.1.1.1.active"], "true"),
            Config(["rtab.main.fib"], "no"),
            Ops(["route.4:main:0.0.0.0/0:1.1.1.1.active"], "false"));

        Assert.True(result.ConfigurationHashChanged);
        Assert.True(result.OperationalHashChanged);
        Assert.True(result.IsConfigurationDrift);
        Assert.True(result.IsOperationalChange);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.ConfigurationDrift);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.OperationalChange);
    }

    [Fact]
    public async Task Ac7UpsertRoundTripPersistsDriftFindings()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        RoutingConfigurationSnapshot baselineConfig = Config(["rtab.main.fib"], "yes");
        RoutingOperationalSnapshot baselineOps = Ops(["route.4:main:0.0.0.0/0:1.1.1.1.active"], "true");

        ApplicationResult<RoutingAssuranceStateView> first = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = baselineConfig,
                OperationalState = baselineOps,
            });
        Assert.True(first.IsSuccess);

        ApplicationResult<RoutingAssuranceStateView> second = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = baselineConfig,
                OperationalState = Ops(["route.4:main:0.0.0.0/0:1.1.1.1.active"], "false"),
            });
        Assert.True(second.IsSuccess);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Contains(persisted!.RouteFindings, f => f.Code == RoutingDriftCodes.OperationalChange);
        Assert.Contains(persisted.RouteFindings, f => f.Code == RoutingDriftCodes.ActiveRouteChanged);
        Assert.DoesNotContain(persisted.RouteFindings, f => f.Code == RoutingDriftCodes.ConfigurationDrift);
    }

    [Fact]
    public void Ac8NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/settings/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/vrf/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/filter/rule/set"));
        Assert.DoesNotContain(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs.Write", StringComparison.Ordinal));
    }

    private static Device CreateDevice()
        => Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.1", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);

    private static RoutingConfigurationSnapshot Config(IEnumerable<(string Key, string Value)> entries)
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal);
        foreach ((string key, string value) in entries)
        {
            material[key] = value;
        }

        return new RoutingConfigurationSnapshot([], RoutingSettingsFact.Empty, [], [], [], [], [], material);
    }

    private static RoutingConfigurationSnapshot Config(IEnumerable<string> keys, string value)
        => Config(keys.Select(k => (k, value)));

    private static RoutingOperationalSnapshot OpsMaterial(Dictionary<string, string> material)
        => new([], [], material);

    private static RoutingOperationalSnapshot Ops(IEnumerable<(string Key, string Value)> entries)
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal);
        foreach ((string key, string value) in entries)
        {
            material[key] = value;
        }

        return new RoutingOperationalSnapshot([], [], material);
    }

    private static RoutingOperationalSnapshot Ops(IEnumerable<string> keys, string value)
        => Ops(keys.Select(k => (k, value)));
}
