using System.Reflection;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Living Spec matrix for Issue Set M7.1-02 AC (RoutingAssuranceState persistence).</summary>
public sealed class RoutingAssuranceStateLivingSpecTests
{
    [Fact]
    public async Task Ac1RoundTripPersistsConfigurationAndOperationalSnapshots()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        RoutingConfigurationSnapshot configuration = SampleConfiguration();
        RoutingOperationalSnapshot operational = SampleOperational();
        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock);
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
            });
        Assert.True(written.IsSuccess, written.Error?.Message);

        ApplicationResult<RoutingAssuranceStateView> loaded = await new GetRoutingAssuranceStateUseCase(auth, store)
            .ExecuteAsync(new GetRoutingAssuranceStateQuery { Actor = "tester", DeviceId = device.Id.Value });
        Assert.True(loaded.IsSuccess);
        Assert.Equal(written.Value!.ConfigurationHashHex, loaded.Value!.ConfigurationHashHex);
        Assert.Equal(written.Value.OperationalHashHex, loaded.Value.OperationalHashHex);
        Assert.Equal(1, loaded.Value.ConfigurationTableCount);
        Assert.Equal(1, loaded.Value.ConfigurationVrfCount);
        Assert.Equal(1, loaded.Value.OperationalDefaultRouteCount);
        Assert.Equal(1UL, loaded.Value.RowVersion);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Equal("main", persisted!.Configuration.Tables[0].Name);
        Assert.Equal("corp", persisted.Configuration.Vrfs[0].Name);
        Assert.Equal("reachable", persisted.OperationalState.DefaultRoutes[0].GatewayStatus);
    }

    [Fact]
    public void Ac2ConfigurationHashDiffersFromOperationalHash()
    {
        RoutingAssuranceState state = RoutingAssuranceState.Create(
            DeviceId.New(),
            SampleConfiguration(),
            SampleOperational(),
            DateTimeOffset.UtcNow);
        Assert.NotEqual(state.ConfigurationHash.ToString(), state.OperationalHash.ToString());
        Assert.NotEmpty(state.Configuration.HashMaterial);
        Assert.NotEmpty(state.OperationalState.HashMaterial);
        Assert.DoesNotContain(
            state.Configuration.HashMaterial.Keys,
            static k => RoutingAssurancePropertyClassifier.ClassifyMaterialKey(k)
                        == RoutingAssurancePropertyKind.Observation);
    }

    [Fact]
    public void Ac3DeferredSlotsExistAsEmptyTypedCollections()
    {
        RoutingAssuranceState state = RoutingAssuranceState.Create(
            DeviceId.New(),
            RoutingConfigurationSnapshot.Empty,
            RoutingOperationalSnapshot.Empty,
            DateTimeOffset.UtcNow);
        Assert.NotNull(state.RouteExpectations);
        Assert.NotNull(state.RouteFindings);
        Assert.NotNull(state.ResolutionTraces);
        Assert.Empty(state.RouteExpectations);
        Assert.Empty(state.RouteFindings);
        Assert.Empty(state.ResolutionTraces);

        // Types exist for later M7.1-* issues (expectations=06, traces=03).
        Assert.NotNull(typeof(RouteExpectation).GetProperty(nameof(RouteExpectation.DestinationPrefix)));
        Assert.NotNull(typeof(RouteFinding).GetProperty(nameof(RouteFinding.Code)));
        Assert.NotNull(typeof(RouteResolutionTrace).GetProperty(nameof(RouteResolutionTrace.Decision)));
    }

    [Fact]
    public void Ac4NoRoutingWriteApisOpened()
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

    [Theory]
    [InlineData("distance", RoutingAssurancePropertyKind.Configuration)]
    [InlineData("disabled", RoutingAssurancePropertyKind.Configuration)]
    [InlineData("policy-rules", RoutingAssurancePropertyKind.Configuration)]
    [InlineData("active", RoutingAssurancePropertyKind.Observation)]
    [InlineData("gateway-status", RoutingAssurancePropertyKind.Observation)]
    [InlineData("immediate-gw", RoutingAssurancePropertyKind.Observation)]
    [InlineData("hw-offloaded", RoutingAssurancePropertyKind.Observation)]
    public void Ac5ClassifierSeparatesConfigFromObservation(string property, RoutingAssurancePropertyKind expected)
        => Assert.Equal(expected, RoutingAssurancePropertyClassifier.ClassifyPropertyName(property));

    [Fact]
    public void Ac6DiscoveryMapsSettingsVrfFiltersIntoAssuranceState()
    {
        RoutingDependencyDiscoveryResult discovery = RoutingDependencyDiscovery.BuildResult(
            Ok(RosReadCommandId.RoutingTables, Row(("name", "main"), ("fib", "yes"))),
            Ok(RosReadCommandId.RoutingSettings, Row(("policy-rules", "lookup"), ("single-process", "yes"))),
            Ok(RosReadCommandId.RoutingRules),
            Ok(RosReadCommandId.IpVrfs, Row(("name", "corp"), ("interfaces", "vlan10"), ("disabled", "false"))),
            Ok(
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
            Ok(RosReadCommandId.Ipv6StaticRoutes),
            Ok(
                RosReadCommandId.Ipv4DefaultRouteState,
                Row(
                    ("dst-address", "0.0.0.0/0"),
                    ("gateway", "1.1.1.1"),
                    ("active", "true"),
                    ("gateway-status", "reachable"),
                    ("immediate-gw", "1.1.1.1%ether1"),
                    ("static", "true"))),
            Ok(RosReadCommandId.Ipv6DefaultRouteState),
            Ok(RosReadCommandId.RoutingFilterRules, Row(("chain", "bgp-in"), ("rule", "accept"), ("disabled", "false"))),
            Ok(RosReadCommandId.RoutingFilterSelectRules),
            Ok(RosReadCommandId.Ipv4Nat),
            Ok(RosReadCommandId.Ipv6Nat),
            Ok(RosReadCommandId.Ipv4Raw),
            Ok(RosReadCommandId.Ipv6Raw),
            Ok(RosReadCommandId.Ipv4Mangle),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("rp-filter", "no"))),
            Ok(RosReadCommandId.Ipv6Settings));

        RoutingAssuranceState state = RoutingAssuranceStateMapper.ToState(
            DeviceId.New(),
            discovery,
            DateTimeOffset.UtcNow);
        Assert.Equal("lookup", state.Configuration.Settings.PolicyRules);
        Assert.Equal("corp", Assert.Single(state.Configuration.Vrfs).Name);
        Assert.Equal("accept", Assert.Single(state.Configuration.FilterRules).Rule);
        Assert.Contains("rsettings.policy-rules", state.Configuration.HashMaterial.Keys);
        Assert.Contains("vrf.corp.interfaces", state.Configuration.HashMaterial.Keys);
        Assert.Contains(state.OperationalState.HashMaterial.Keys, k => k.Contains("gateway-status", StringComparison.Ordinal));
        Assert.NotEqual(state.ConfigurationHash, state.OperationalHash);
        Assert.Empty(state.RouteExpectations);
        Assert.Empty(state.ResolutionTraces);
    }

    [Fact]
    public async Task Ac7UpsertIsForbiddenWithoutWritePermission()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryWrite);
        UpsertRoutingAssuranceStateUseCase useCase = new(
            auth,
            new FakeDeviceStore(),
            new FakeRoutingAssuranceStateStore(),
            new FakeClock());
        ApplicationResult<RoutingAssuranceStateView> result = await useCase.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = Guid.NewGuid(),
                Configuration = RoutingConfigurationSnapshot.Empty,
                OperationalState = RoutingOperationalSnapshot.Empty,
            });
        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public void Ac8EntityHasForeignKeyToDevicePattern()
    {
        Type entity = typeof(Mfc.Infrastructure.Persistence.Entities.RoutingAssuranceStateEntity);
        Assert.NotNull(entity.GetProperty("DeviceId"));
        Assert.NotNull(entity.GetProperty("ConfigurationHash"));
        Assert.NotNull(entity.GetProperty("OperationalHash"));
        Assert.NotNull(entity.GetProperty("ConfigurationJson"));
        Assert.NotNull(entity.GetProperty("OperationalJson"));
        Assert.NotNull(entity.GetProperty("RouteExpectationsJson"));
        Assert.NotNull(entity.GetProperty("RouteFindingsJson"));
        Assert.NotNull(entity.GetProperty("ResolutionTracesJson"));

        string configSource = File.ReadAllText(Path.Combine(RepoRoot, "src", "Mfc.Infrastructure", "Persistence", "Configurations", "RoutingAssuranceStateConfiguration.cs"));
        Assert.Contains("routing_assurance_states", configSource, StringComparison.Ordinal);
        Assert.Contains("HasForeignKey", configSource, StringComparison.Ordinal);
        Assert.Contains("DeviceEntity", configSource, StringComparison.Ordinal);
    }

    private static string RepoRoot
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Repository root not found.");
        }
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

    private static RoutingConfigurationSnapshot SampleConfiguration()
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["rtab.main.fib"] = "yes",
            ["rsettings.policy-rules"] = "lookup",
            ["vrf.corp.interfaces"] = "vlan10",
            ["route.4:main:0.0.0.0/0:1.1.1.1.distance"] = "1",
            ["filter.0.rule"] = "accept",
        };
        return new RoutingConfigurationSnapshot(
            [new RoutingTableFact { Name = "main", Fib = "yes", Disabled = "false" }],
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
            [],
            [new VrfDefinitionFact { Name = "corp", Interfaces = "vlan10", Disabled = "false" }],
            [
                new StaticRouteConfigFact
                {
                    Family = "ipv4",
                    DstAddress = "0.0.0.0/0",
                    Gateway = "1.1.1.1",
                    RoutingTable = "main",
                    Distance = 1,
                    Scope = 30,
                    TargetScope = 10,
                    PrefSrc = null,
                    CheckGateway = null,
                    Disabled = "false",
                },
            ],
            [new RouteFilterRuleFact { EffectiveOrdinal = 0, Chain = "bgp-in", Rule = "accept", Disabled = "false" }],
            [],
            material);
    }

    private static RoutingOperationalSnapshot SampleOperational()
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["route.4:main:0.0.0.0/0:1.1.1.1.active"] = "true",
            ["route.4:main:0.0.0.0/0:1.1.1.1.gateway-status"] = "reachable",
            ["default.4:main:1.1.1.1.active"] = "true",
        };
        return new RoutingOperationalSnapshot(
            [
                new RouteObservationFact
                {
                    Family = "ipv4",
                    DstAddress = "0.0.0.0/0",
                    RoutingTable = "main",
                    Gateway = "1.1.1.1",
                    Active = "true",
                    ImmediateGateway = "1.1.1.1%ether1",
                    GatewayStatus = "reachable",
                    IsDynamic = false,
                    HwOffloaded = null,
                },
            ],
            [
                new DefaultRouteObservationFact
                {
                    Family = "ipv4",
                    DstAddress = "0.0.0.0/0",
                    RoutingTable = "main",
                    Gateway = "1.1.1.1",
                    Distance = 1,
                    Active = "true",
                    ImmediateGateway = "1.1.1.1%ether1",
                    GatewayStatus = "reachable",
                    IsDynamic = false,
                    IsStatic = true,
                },
            ],
            material);
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
