using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Extra branch coverage for M7.1-09 routing drift modules.</summary>
public sealed class RoutingDriftCoverageTests
{
    [Fact]
    public void AnalyzeReturnsNoneWhenHashesMatch()
    {
        RoutingConfigurationSnapshot configuration = SnapshotConfig("yes");
        RoutingOperationalSnapshot operational = SnapshotOps("true");
        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            configuration,
            operational,
            configuration,
            operational);
        Assert.False(result.IsConfigurationDrift);
        Assert.False(result.IsOperationalChange);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ClassifierMapsConfigurationKeyPrefixes()
    {
        Assert.Equal(
            RoutingDriftKind.RoutingRuleChanged,
            RoutingDriftClassifier.ClassifyMaterialKey("rrule.3.disabled", isConfigurationMaterial: true));
        Assert.Equal(
            RoutingDriftKind.VrfBindingChanged,
            RoutingDriftClassifier.ClassifyMaterialKey("vrf.corp.interfaces", isConfigurationMaterial: true));
        Assert.Equal(
            RoutingDriftKind.RouteFilterChanged,
            RoutingDriftClassifier.ClassifyMaterialKey("filter-select.1.rule", isConfigurationMaterial: true));
        Assert.Equal(
            RoutingDriftKind.FirewallRoutingDependencyChanged,
            RoutingDriftClassifier.ClassifyMaterialKey("mangle4.2.routing-mark", isConfigurationMaterial: true));
        Assert.True(RoutingDriftClassifier.IsConfigurationMaterialKey("ip4.rp-filter"));
        Assert.False(RoutingDriftClassifier.IsConfigurationMaterialKey("default.4:main:1.1.1.1.active"));
    }

    [Fact]
    public void ClassifierMapsOperationalObservationKeys()
    {
        Assert.Equal(
            RoutingDriftKind.EcmpMemberChanged,
            RoutingDriftClassifier.ClassifyOperationalChange(
                "route.4:main:10.0.0.0/8:10.1.1.1.immediate-gw",
                "10.1.1.1%ether1",
                "10.1.1.2%ether2"));
        Assert.Equal(
            RoutingDriftKind.DynamicBestPathChanged,
            RoutingDriftClassifier.ClassifyOperationalChange("route.4:main:0.0.0.0/0:1.1.1.1.type", "bgp", "ospf"));
        Assert.Equal(
            RoutingDriftKind.RouteExecutionPathChanged,
            RoutingDriftClassifier.ClassifyOperationalChange(
                "route.4:main:0.0.0.0/0:1.1.1.1.hw-offloaded",
                "false",
                "true"));
    }

    [Fact]
    public void CodeForKindThrowsForUnknownKind()
        => Assert.Throws<ArgumentOutOfRangeException>(() => RoutingDriftCodes.CodeForKind((RoutingDriftKind)99));

    [Fact]
    public void AnalyzeFromPersistedStateDetectsSettingsDrift()
    {
        RoutingAssuranceState previous = RoutingAssuranceState.Create(
            DeviceId.New(),
            SnapshotConfig("yes", settings: "lookup"),
            SnapshotOps("true"),
            DateTimeOffset.UtcNow);
        RoutingConfigurationSnapshot currentConfig = SnapshotConfig("yes", settings: "only");
        RoutingDriftClassification result = RoutingDriftAnalyzer.Analyze(
            previous,
            currentConfig,
            SnapshotOps("true"));

        Assert.True(result.IsConfigurationDrift);
        Assert.Contains(result.Findings, f => f.Code == RoutingDriftCodes.RoutingSettingsChanged);
    }

    [Fact]
    public async Task UpsertMergeDriftFindingsDedupesByCodeAndSubject()
    {
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = Device.Reconstitute(
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
        await devices.AddAsync(device);

        UpsertRoutingAssuranceStateUseCase upsert = new(new FakeAuthorizationBoundary(), devices, store, clock);
        RoutingConfigurationSnapshot config = SnapshotConfig("yes");
        RoutingOperationalSnapshot ops = SnapshotOps("true");

        await upsert.ExecuteAsync(new UpsertRoutingAssuranceStateCommand
        {
            Actor = "tester",
            DeviceId = device.Id.Value,
            Configuration = config,
            OperationalState = ops,
        });

        RouteFinding manual = new()
        {
            Code = RoutingDriftCodes.ActiveRouteChanged,
            Message = "manual",
            Subject = "route.4:main:0.0.0.0/0:1.1.1.1.active",
        };
        await upsert.ExecuteAsync(new UpsertRoutingAssuranceStateCommand
        {
            Actor = "tester",
            DeviceId = device.Id.Value,
            Configuration = config,
            OperationalState = SnapshotOps("false"),
            RouteFindings = [manual],
        });

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Equal(1, persisted!.RouteFindings.Count(f =>
            f.Code == RoutingDriftCodes.ActiveRouteChanged
            && f.Subject == "route.4:main:0.0.0.0/0:1.1.1.1.active"));
    }

    [Fact]
    public void GatewayStatusReachableChangeMapsToActiveRouteChanged()
    {
        RoutingDriftKind kind = RoutingDriftClassifier.ClassifyOperationalChange(
            "route.4:main:0.0.0.0/0:1.1.1.1.gateway-status",
            "checking",
            "reachable");
        Assert.Equal(RoutingDriftKind.ActiveRouteChanged, kind);
    }

    private static RoutingConfigurationSnapshot SnapshotConfig(string fib, string settings = "lookup")
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["rtab.main.fib"] = fib,
            ["rsettings.policy-rules"] = settings,
        };
        return new RoutingConfigurationSnapshot([], RoutingSettingsFact.Empty, [], [], [], [], [], material);
    }

    private static RoutingOperationalSnapshot SnapshotOps(string active)
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["route.4:main:0.0.0.0/0:1.1.1.1.active"] = active,
        };
        return new RoutingOperationalSnapshot([], [], material);
    }
}
