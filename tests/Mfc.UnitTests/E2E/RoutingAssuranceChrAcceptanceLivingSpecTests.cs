using System.Reflection;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.E2E;

/// <summary>
/// Living Spec matrix for Issue Set M7.1-11 AC 1–10 (CHR acceptance: multi-WAN recursive, ECMP, VRF).
/// Scripted in-process fixtures ONLY — live CHR matrix remains OFF.
/// Chains M7.1 modules end-to-end via <see cref="UpsertRoutingAssuranceStateUseCase"/> / domain analyzers.
/// </summary>
public sealed class RoutingAssuranceChrAcceptanceLivingSpecTests
{
    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-WAN recursive: policy routing mark → table lookup → recursive gateway resolution trace succeeds.
    /// </summary>
    [Fact]
    public async Task Ac1MultiWanRecursivePolicyMarkTableLookupAndRecursiveGatewaySucceeds()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = MultiWanRecursiveConfiguration();
        RoutingOperationalSnapshot operational = MultiWanRecursiveOperational();

        RouteResolutionQuery query = new()
        {
            Family = "ipv4",
            SourceAddress = "10.1.0.5",
            DestinationAddress = "10.50.0.20",
            RoutingMark = "wan-primary-mark",
            MatchedMangleRule = new MatchedMangleRule
            {
                Ordinal = 2,
                Chain = "prerouting",
                AssignedRoutingMark = "wan-primary-mark",
            },
        };

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                TraceQueries = [query],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);

        Assert.Equal("wan-primary-mark", trace.RoutingMark);
        Assert.Equal("wan-primary", trace.SelectedTable);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.NotEmpty(trace.RecursiveResolution);
        Assert.Contains(trace.RecursiveResolution, s => s.Target == "10.0.0.254");
        Assert.Equal("ether1", Assert.Single(trace.EgressInterfaces));
        Assert.Empty(persisted.RouteFindings);
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>Balanced/per-table multi-WAN: traces resolve per routing table (wan-primary / wan-backup).</summary>
    [Fact]
    public async Task Ac2BalancedPerTableMultiWanTracesResolvePerRoutingTable()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = BalancedMultiWanConfiguration();
        RoutingOperationalSnapshot operational = BalancedMultiWanOperational();

        RouteResolutionQuery primary = new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.10",
            RoutingMark = "wan-primary-mark",
        };
        RouteResolutionQuery backup = new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.11",
            RoutingMark = "wan-backup-mark",
        };

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                TraceQueries = [primary, backup],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value!.ResolutionTraceCount);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.ResolutionTraces.Count);

        RouteResolutionTrace primaryTrace = persisted.ResolutionTraces.Single(t => t.DestinationAddress == "203.0.113.10");
        RouteResolutionTrace backupTrace = persisted.ResolutionTraces.Single(t => t.DestinationAddress == "203.0.113.11");

        Assert.Equal("wan-primary", primaryTrace.SelectedTable);
        Assert.Equal("198.51.100.1", primaryTrace.SelectedRoutes[0].Gateway);
        Assert.Equal("ether1", Assert.Single(primaryTrace.EgressInterfaces));

        Assert.Equal("wan-backup", backupTrace.SelectedTable);
        Assert.Equal("203.0.113.1", backupTrace.SelectedRoutes[0].Gateway);
        Assert.Equal("ether2", Assert.Single(backupTrace.EgressInterfaces));
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>ECMP: <see cref="EcmpRouteSet"/> ONE_OF + expectation with allowed next hops passes when any member matches.</summary>
    [Fact]
    public async Task Ac3EcmpOneOfExpectationPassesWhenAnyMemberMatches()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = EcmpConfiguration();
        RoutingOperationalSnapshot operational = EcmpOperational();

        RouteExpectation expectation = Expectation(
            "10.80.0.0/16",
            allowedNextHops: ["10.0.0.3"]);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                RouteExpectations = [expectation],
                TraceQueries = [Query("10.80.0.10")],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0, result.Value!.RouteFindingCount);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);
        Assert.NotNull(trace.EcmpRouteSet);
        Assert.Equal(2, trace.EcmpRouteSet!.NextHops.Count);
        Assert.All(trace.ImmediateNextHops, h => Assert.Equal(ImmediateNextHopSelectors.OneOf, h.Selector));
        Assert.Equal(RouteResolutionCertainties.Indeterminate, trace.Certainty);
        Assert.Empty(persisted.RouteFindings);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>VRF: corp VRF trace + expectation (expected_vrf/table) passes.</summary>
    [Fact]
    public async Task Ac4CorpVrfTraceAndExpectationPasses()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = CorpVrfConfiguration();
        RoutingOperationalSnapshot operational = CorpVrfOperational();

        RouteResolutionQuery query = new()
        {
            Family = "ipv4",
            SourceAddress = "10.20.0.5",
            DestinationAddress = "10.20.0.50",
            IngressInterface = "vlan10",
        };
        RouteExpectation expectation = Expectation(
            "10.20.0.0/16",
            expectedTable: "corp",
            expectedVrf: "corp");

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                RouteExpectations = [expectation],
                TraceQueries = [query],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0, result.Value!.RouteFindingCount);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);
        Assert.Equal("corp", trace.SelectedTable);
        Assert.Equal("corp", trace.SelectedVrf);
        Assert.Equal("ipsec1", Assert.Single(trace.EgressInterfaces));
        Assert.Empty(persisted.RouteFindings);
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Expectation fail: wrong table/VRF/egress produces <see cref="RouteFinding"/> (critical when configured).</summary>
    [Fact]
    public async Task Ac5ExpectationFailWrongTableVrfEgressProducesCriticalFindings()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = CorpVrfConfiguration();
        RoutingOperationalSnapshot operational = CorpVrfOperational();

        RouteExpectation[] expectations =
        [
            Expectation("10.20.0.0/16", expectedTable: "main", critical: true),
            Expectation("10.20.0.0/16", expectedVrf: "main", critical: true),
            Expectation("10.20.0.0/16", allowedEgressInterfaces: ["ether2"], critical: true),
        ];

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                RouteExpectations = expectations,
                TraceQueries =
                [
                    new RouteResolutionQuery
                    {
                        Family = "ipv4",
                        SourceAddress = "10.20.0.5",
                        DestinationAddress = "10.20.0.50",
                        IngressInterface = "vlan10",
                    },
                ],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(3, result.Value!.RouteFindingCount);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Contains(persisted!.RouteFindings, f => f.Code == RouteExpectationCodes.ExpectedTableMismatchCritical);
        Assert.Contains(persisted.RouteFindings, f => f.Code == RouteExpectationCodes.ExpectedVrfMismatchCritical);
        Assert.Contains(persisted.RouteFindings, f => f.Code == RouteExpectationCodes.AllowedEgressInterfaceViolationCritical);
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>Operational route change (active gateway) → operational drift finding, NOT configuration drift.</summary>
    [Fact]
    public async Task Ac6OperationalRouteChangeProducesOperationalDriftNotConfigurationDrift()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = BalancedMultiWanConfiguration();
        RoutingOperationalSnapshot baseline = OpsMaterial(
        [
            ("route.4:wan-primary:0.0.0.0/0:198.51.100.1.active", "true"),
            ("route.4:wan-backup:0.0.0.0/0:203.0.113.1.active", "true"),
        ]);
        RoutingOperationalSnapshot shifted = OpsMaterial(
        [
            ("route.4:wan-primary:0.0.0.0/0:198.51.100.1.active", "false"),
            ("route.4:wan-backup:0.0.0.0/0:203.0.113.1.active", "true"),
        ]);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        Assert.True((await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = baseline,
            })).IsSuccess);

        ApplicationResult<RoutingAssuranceStateView> second = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = shifted,
            });
        Assert.True(second.IsSuccess, second.Error?.Message);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Contains(persisted!.RouteFindings, f => f.Code == RoutingDriftCodes.OperationalChange);
        Assert.Contains(persisted.RouteFindings, f => f.Code == RoutingDriftCodes.ActiveRouteChanged);
        Assert.DoesNotContain(persisted.RouteFindings, f => f.Code == RoutingDriftCodes.ConfigurationDrift);
        Assert.Equal(configuration.HashMaterial, persisted.Configuration.HashMaterial);
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reverse-path symmetry + network path probe binding on representative branch→HQ trace (scripted).
    /// </summary>
    [Fact]
    public async Task Ac7ReversePathSymmetryAndNetworkPathProbeBindingOnBranchToHqTrace()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = BranchToHqConfiguration();
        RoutingOperationalSnapshot operational = BranchToHqOperational();

        RouteResolutionQuery query = new()
        {
            Family = "ipv4",
            SourceAddress = "192.168.1.50",
            DestinationAddress = "10.10.0.20",
        };
        NetworkPathProfile profile = new()
        {
            SourceDevice = device.Id,
            Destination = "10.10.0.20",
            RoutingTable = "wan-primary",
            Vrf = "main",
            SourceInterface = "ether2",
            MaxRtt = 100,
        };

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                TraceQueries = [query],
                NetworkPathProfiles = [profile],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);

        Assert.NotNull(trace.ReversePathSymmetry);
        Assert.Equal(ReversePathSymmetryResults.Symmetric, trace.ReversePathSymmetry!.Result);
        Assert.NotNull(trace.ReversePathSymmetry.ReverseTrace);
        Assert.Equal("main", trace.ReversePathSymmetry.ReverseTrace!.SelectedTable);

        NetworkPathProbeBinding binding = Assert.Single(trace.NetworkPathProbeBindings);
        Assert.Equal("10.10.0.20", binding.Probe.Destination);
        Assert.Equal("main", binding.Probe.RoutingTable);
        Assert.Equal("main", binding.Probe.SelectedVrf);
        Assert.Equal("ether1", binding.Probe.Interface);
        Assert.NotEqual(profile.RoutingTable, binding.Probe.RoutingTable);
        Assert.NotEqual(profile.SourceInterface, binding.Probe.Interface);

        DeploymentProbe deploymentProbe = binding.Probe.ToDeploymentProbe();
        Assert.Equal("main", deploymentProbe.RoutingTable);
        Assert.Equal("ether1", deploymentProbe.Interface);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>Full upsert round-trip persists expectations/findings/traces/drift findings.</summary>
    [Fact]
    public async Task Ac8FullUpsertRoundTripPersistsExpectationsFindingsTracesAndDrift()
    {
        (FakeAuthorizationBoundary auth, FakeDeviceStore devices, FakeRoutingAssuranceStateStore store, FakeClock clock, Device device) =
            await SeedDeviceAsync();
        RoutingConfigurationSnapshot configuration = BranchToHqConfiguration();
        RoutingOperationalSnapshot baseline = BranchToHqOperational(gatewayStatus: "reachable");

        RouteExpectation expectation = Expectation(
            "10.10.0.0/16",
            expectedTable: "main",
            allowedNextHops: ["10.0.0.1"],
            requireReversePath: true);

        RouteResolutionQuery query = new()
        {
            Family = "ipv4",
            SourceAddress = "192.168.1.50",
            DestinationAddress = "10.10.0.20",
        };

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        Assert.True((await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = baseline,
                RouteExpectations = [expectation],
                TraceQueries = [query],
            })).IsSuccess);

        ApplicationResult<RoutingAssuranceStateView> second = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = BranchToHqOperational(gatewayStatus: "unreachable"),
                RouteExpectations = [expectation],
                TraceQueries = [query],
            });
        Assert.True(second.IsSuccess, second.Error?.Message);

        ApplicationResult<RoutingAssuranceDetailView> loaded = await new GetRoutingAssuranceStateUseCase(auth, store)
            .ExecuteAsync(new GetRoutingAssuranceStateQuery { Actor = "tester", DeviceId = device.Id.Value });
        Assert.True(loaded.IsSuccess);
        Assert.Single(loaded.Value!.Expectations);
        Assert.NotEmpty(loaded.Value.Findings);
        Assert.Single(loaded.Value.TraceSummaries);
        Assert.Contains(loaded.Value.Findings, f => f.Code == RoutingDriftCodes.OperationalChange);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Single(persisted!.RouteExpectations);
        Assert.NotEmpty(persisted.RouteFindings);
        RouteResolutionTrace trace = Assert.Single(persisted.ResolutionTraces);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.Equal("main", trace.SelectedTable);
        Assert.NotNull(trace.ReversePathSymmetry);
    }

    private static RoutingOperationalSnapshot OpsMaterial(IEnumerable<(string Key, string Value)> entries)
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal);
        foreach ((string key, string value) in entries)
        {
            material[key] = value;
        }

        return new RoutingOperationalSnapshot([], [], material);
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>No routing write APIs (<see cref="RosReadCommandRegistry"/> check).</summary>
    [Fact]
    public void Ac9NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/settings/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/vrf/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/filter/rule/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/set"));
        Assert.DoesNotContain(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs.Write", StringComparison.Ordinal));
        Assert.Null(typeof(UpsertRoutingAssuranceStateUseCase).GetMethod(
            "AddRoute",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>Suite documents live CHR OFF (source comment + final AC asserts no live CHR dependency).</summary>
    [Fact]
    public void Ac10DeterministicLivingSpecNoLiveChr()
    {
        string root = RepoRoot;
        string thisFile = Path.Combine(root, "tests", "Mfc.UnitTests", "E2E", "RoutingAssuranceChrAcceptanceLivingSpecTests.cs");
        Assert.True(File.Exists(thisFile), thisFile);
        string source = File.ReadAllText(thisFile);
        Assert.Contains("live CHR matrix remains OFF", source, StringComparison.Ordinal);
        Assert.Contains("Scripted in-process fixtures ONLY", source, StringComparison.Ordinal);
        string forbiddenLiveToken = string.Concat("Connect", "ToLive", "Chr");
        Assert.DoesNotContain(forbiddenLiveToken, source, StringComparison.Ordinal);

        string testing = File.ReadAllText(Path.Combine(root, "docs", "development", "testing.md"));
        Assert.Contains("M7.1-11", testing, StringComparison.Ordinal);
        Assert.Contains("RoutingAssuranceChrAcceptanceLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Live CHR", testing, StringComparison.OrdinalIgnoreCase);

        string readme = File.ReadAllText(Path.Combine(root, "testlab", "chr", "README.md"));
        Assert.Contains("routing-assurance-multiwan", readme, StringComparison.Ordinal);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    private static RoutingConfigurationSnapshot MultiWanRecursiveConfiguration()
        => Config(
            tables: [Table("main"), Table("wan-primary")],
            rules:
            [
                Rule(0, RoutingRuleActions.Lookup, routingMark: "wan-primary-mark", table: "wan-primary"),
            ],
            staticRoutes:
            [
                Route("10.50.0.0/16", "10.0.0.254", "wan-primary", scope: 30, targetScope: 10),
                Route("10.0.0.0/24", "10.0.0.1", "wan-primary", scope: 10, targetScope: 10),
                Route("0.0.0.0/0", "198.51.100.1", "wan-primary"),
                Route("0.0.0.0/0", "1.1.1.1", "main"),
            ]);

    private static RoutingOperationalSnapshot MultiWanRecursiveOperational()
        => Ops(
        [
            Obs("10.50.0.0/16", "10.0.0.254", "wan-primary"),
            Obs("10.0.0.0/24", "10.0.0.1", "wan-primary", immediateGw: "10.0.0.1%ether1"),
            Obs("0.0.0.0/0", "198.51.100.1", "wan-primary", immediateGw: "198.51.100.1%ether1"),
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether2"),
        ]);

    private static RoutingConfigurationSnapshot BalancedMultiWanConfiguration()
        => Config(
            tables: [Table("main"), Table("wan-primary"), Table("wan-backup")],
            rules:
            [
                Rule(0, RoutingRuleActions.Lookup, routingMark: "wan-primary-mark", table: "wan-primary"),
                Rule(1, RoutingRuleActions.Lookup, routingMark: "wan-backup-mark", table: "wan-backup"),
            ],
            staticRoutes:
            [
                Route("0.0.0.0/0", "198.51.100.1", "wan-primary"),
                Route("0.0.0.0/0", "203.0.113.1", "wan-backup"),
            ]);

    private static RoutingOperationalSnapshot BalancedMultiWanOperational(
        string activePrimary = "true",
        string activeBackup = "true")
        => Ops(
        [
            Obs("0.0.0.0/0", "198.51.100.1", "wan-primary", immediateGw: "198.51.100.1%ether1", active: activePrimary),
            Obs("0.0.0.0/0", "203.0.113.1", "wan-backup", immediateGw: "203.0.113.1%ether2", active: activeBackup),
        ]);

    private static RoutingConfigurationSnapshot EcmpConfiguration()
        => Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.80.0.0/16", "10.0.0.2", "main"),
                Route("10.80.0.0/16", "10.0.0.3", "main"),
            ]);

    private static RoutingOperationalSnapshot EcmpOperational()
        => Ops(
        [
            Obs("10.80.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
            Obs("10.80.0.0/16", "10.0.0.3", "main", immediateGw: "10.0.0.3%ether2"),
        ]);

    private static RoutingConfigurationSnapshot CorpVrfConfiguration()
        => Config(
            tables: [Table("main"), Table("corp")],
            vrfs: [Vrf("corp", "vlan10")],
            staticRoutes:
            [
                Route("0.0.0.0/0", "1.1.1.1", "main"),
                Route("10.20.0.0/16", "10.99.0.1", "corp"),
            ]);

    private static RoutingOperationalSnapshot CorpVrfOperational()
        => Ops(
        [
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
            Obs("10.20.0.0/16", "10.99.0.1", "corp", immediateGw: "10.99.0.1%ipsec1"),
        ]);

    private static RoutingConfigurationSnapshot BranchToHqConfiguration()
        => Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.10.0.0/16", "10.0.0.1", "main"),
                Route("192.168.0.0/16", "10.0.0.2", "main"),
            ]);

    private static RoutingOperationalSnapshot BranchToHqOperational(string gatewayStatus = "reachable")
    {
        RouteObservationFact[] routes =
        [
            Obs("10.10.0.0/16", "10.0.0.1", "main", immediateGw: "10.0.0.1%ether1"),
            Obs("192.168.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
        ];
        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["route.4:main:10.10.0.0/16:10.0.0.1.gateway-status"] = "reachable",
            ["route.4:main:192.168.0.0/16:10.0.0.2.gateway-status"] = gatewayStatus,
        };
        return new RoutingOperationalSnapshot(routes, [], material);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static async Task<(
        FakeAuthorizationBoundary Auth,
        FakeDeviceStore Devices,
        FakeRoutingAssuranceStateStore Store,
        FakeClock Clock,
        Device Device)> SeedDeviceAsync()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);
        return (auth, devices, store, clock, device);
    }

    private static RouteExpectation Expectation(
        string destinationPrefix,
        string? expectedVrf = null,
        string? expectedTable = null,
        IReadOnlyList<string>? allowedNextHops = null,
        IReadOnlyList<string>? allowedEgressInterfaces = null,
        bool requireReversePath = false,
        bool critical = false)
        => new()
        {
            NodeId = null,
            Family = "ipv4",
            DestinationPrefix = destinationPrefix,
            ExpectedVrf = expectedVrf,
            ExpectedTable = expectedTable,
            AllowedNextHops = allowedNextHops ?? [],
            AllowedEgressInterfaces = allowedEgressInterfaces ?? [],
            RequireReversePath = requireReversePath,
            Critical = critical,
        };

    private static RouteResolutionQuery Query(string destination, string? source = "192.168.0.10")
        => new()
        {
            Family = "ipv4",
            SourceAddress = source,
            DestinationAddress = destination,
        };

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static VrfDefinitionFact Vrf(string name, string interfaces)
        => new() { Name = name, Interfaces = interfaces, Disabled = "false" };

    private static RoutingRuleFact Rule(
        int ordinal,
        string action,
        string? routingMark = null,
        string? table = null)
        => new()
        {
            EffectiveOrdinal = ordinal,
            Action = action,
            SrcAddress = null,
            DstAddress = null,
            RoutingMark = routingMark,
            Table = table,
            Disabled = "false",
        };

    private static StaticRouteConfigFact Route(
        string dst,
        string gateway,
        string table,
        int distance = 1,
        int? scope = null,
        int? targetScope = null)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            Gateway = gateway,
            RoutingTable = table,
            Distance = distance,
            Scope = scope,
            TargetScope = targetScope,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
        };

    private static RouteObservationFact Obs(
        string dst,
        string gateway,
        string table,
        string? immediateGw = null,
        string? active = "true")
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = active,
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = false,
            HwOffloaded = null,
        };

    private static RoutingConfigurationSnapshot Config(
        IReadOnlyList<RoutingTableFact> tables,
        IReadOnlyList<RoutingRuleFact>? rules = null,
        IReadOnlyList<VrfDefinitionFact>? vrfs = null,
        IReadOnlyList<StaticRouteConfigFact>? staticRoutes = null)
        => new(
            tables,
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
            rules ?? [],
            vrfs ?? [],
            staticRoutes ?? [],
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingOperationalSnapshot Ops(IReadOnlyList<RouteObservationFact> routes)
        => new(routes, [], new Dictionary<string, string>(StringComparer.Ordinal));

    private static Device CreateDevice()
        => Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("chr-ra"),
            ManagementEndpoint.Create("192.0.2.1", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);

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
}
