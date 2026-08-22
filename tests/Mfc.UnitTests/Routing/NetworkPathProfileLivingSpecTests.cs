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

namespace Mfc.UnitTests.Routing;

/// <summary>Living Spec matrix for Issue Set M7.1-08 AC (NetworkPathProfile latency probes).</summary>
public sealed class NetworkPathProfileLivingSpecTests
{
    [Fact]
    public void Ac1BindTableVrfAndInterfaceFromTraceNotProfileHints()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("corp")],
            vrfs: [Vrf("corp", "vlan10")],
            staticRoutes:
            [
                Route("0.0.0.0/0", "1.1.1.1", "main"),
                Route("10.20.0.0/16", "10.99.0.1", "corp"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
            Obs("10.20.0.0/16", "10.99.0.1", "corp", immediateGw: "10.99.0.1%ipsec1"),
        ]);

        RouteResolutionTrace trace = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = "10.20.0.5",
                DestinationAddress = "10.20.0.50",
                IngressInterface = "vlan10",
            },
            configuration,
            operational);

        NetworkPathProfile profile = Profile(
            destination: "10.20.0.50",
            routingTable: "main",
            vrf: "main",
            sourceInterface: "ether1");

        NetworkPathProbeBinding binding = NetworkPathProfileBinder.Bind(profile, trace);

        Assert.Equal("10.20.0.50", binding.Probe.Destination);
        Assert.Equal("corp", binding.Probe.RoutingTable);
        Assert.Equal("corp", binding.Probe.SelectedVrf);
        Assert.Equal("ipsec1", binding.Probe.Interface);
        Assert.NotEqual(profile.RoutingTable, binding.Probe.RoutingTable);
        Assert.NotEqual(profile.Vrf, binding.Probe.SelectedVrf);
        Assert.NotEqual(profile.SourceInterface, binding.Probe.Interface);

        DeploymentProbe deploymentProbe = binding.Probe.ToDeploymentProbe();
        Assert.Equal("corp", deploymentProbe.RoutingTable);
        Assert.Equal("ipsec1", deploymentProbe.Interface);
    }

    [Fact]
    public void Ac2ProbeDestinationComesFromProfile()
    {
        RouteResolutionTrace trace = SampleTrace(
            destination: "203.0.113.55",
            table: "main",
            egress: "ether1",
            gateway: "1.1.1.1",
            prefix: "203.0.113.0/24");

        NetworkPathProfile profile = Profile(destination: "203.0.113.55");
        NetworkPathProbeBinding binding = NetworkPathProfileBinder.Bind(profile, trace);

        Assert.Equal("203.0.113.55", binding.Probe.Destination);
        Assert.Equal("203.0.113.55", binding.Probe.ToDeploymentProbe().Destination);
    }

    [Fact]
    public void Ac3PathChangeWithLatencyRegressionEmitsCombinedFinding()
    {
        RoutePathFingerprint baseline = new()
        {
            MatchedPrefix = "0.0.0.0/0",
            NextHops = ["1.1.1.1"],
            EgressInterfaces = ["ether1"],
            ExecutionPath = RouteResolutionExecutionPaths.Cpu,
        };
        RouteResolutionTrace currentTrace = SampleTrace(
            destination: "203.0.113.10",
            table: "wan2",
            egress: "ether2",
            gateway: "2.2.2.2",
            prefix: "0.0.0.0/0",
            executionPath: RouteResolutionExecutionPaths.Hardware);

        NetworkPathProfile profile = Profile(
            destination: "203.0.113.10",
            maxRtt: 50,
            maxRegression: 0.10,
            critical: true);

        RouteFinding finding = Assert.Single(
            NetworkPathLatencyEvaluator.Evaluate(
                new NetworkPathLatencyEvaluationInput
                {
                    Profile = profile,
                    Trace = currentTrace,
                    BaselinePathFingerprint = baseline,
                    BaselineMeasurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 20,
                        JitterMs = 1,
                    },
                    Measurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 80,
                        JitterMs = 2,
                    },
                }));

        Assert.Equal(NetworkPathProfileCodes.RoutePathChangedWithLatencyRegressionCritical, finding.Code);
        Assert.DoesNotContain(
            NetworkPathLatencyEvaluator.Evaluate(
                new NetworkPathLatencyEvaluationInput
                {
                    Profile = profile,
                    Trace = currentTrace,
                    BaselinePathFingerprint = baseline,
                    BaselineMeasurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 20,
                        JitterMs = 1,
                    },
                    Measurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 80,
                        JitterMs = 2,
                    },
                }),
            static f => f.Code == NetworkPathProfileCodes.LatencyRttHighCritical);
    }

    [Fact]
    public void Ac4HighLatencyWithoutPathChangeEmitsIsolatedFinding()
    {
        RoutePathFingerprint fingerprint = RoutePathFingerprint.FromTrace(
            SampleTrace(
                destination: "203.0.113.10",
                table: "main",
                egress: "ether1",
                gateway: "1.1.1.1",
                prefix: "0.0.0.0/0"));

        NetworkPathProfile profile = Profile(destination: "203.0.113.10", maxRtt: 40);
        RouteResolutionTrace trace = SampleTrace(
            destination: "203.0.113.10",
            table: "main",
            egress: "ether1",
            gateway: "1.1.1.1",
            prefix: "0.0.0.0/0");

        RouteFinding finding = Assert.Single(
            NetworkPathLatencyEvaluator.Evaluate(
                new NetworkPathLatencyEvaluationInput
                {
                    Profile = profile,
                    Trace = trace,
                    BaselinePathFingerprint = fingerprint,
                    Measurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 120,
                        JitterMs = 3,
                    },
                }));

        Assert.Equal(NetworkPathProfileCodes.LatencyRttHigh, finding.Code);
    }

    [Fact]
    public void Ac5RouteExpectationsPassThroughOnTrace()
    {
        RouteResolutionTrace trace = SampleTrace(
            destination: "203.0.113.10",
            table: "main",
            egress: "ether1",
            gateway: "1.1.1.1",
            prefix: "0.0.0.0/0",
            executionPath: RouteResolutionExecutionPaths.Cpu);

        NetworkPathProfile pass = Profile(
            destination: "203.0.113.10",
            expectedRoutePrefix: "0.0.0.0/0",
            expectedNextHops: ["1.1.1.1"],
            expectedEgressInterfaces: ["ether1"],
            expectedExecutionPath: RouteResolutionExecutionPaths.Cpu);
        Assert.Empty(
            NetworkPathLatencyEvaluator.Evaluate(
                new NetworkPathLatencyEvaluationInput
                {
                    Profile = pass,
                    Trace = trace,
                    Measurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 10,
                        JitterMs = 1,
                    },
                }));

        NetworkPathProfile fail = Profile(
            destination: "203.0.113.10",
            expectedNextHops: ["9.9.9.9"]);
        RouteFinding finding = Assert.Single(
            NetworkPathLatencyEvaluator.Evaluate(
                new NetworkPathLatencyEvaluationInput
                {
                    Profile = fail,
                    Trace = trace,
                    Measurement = new LatencyMeasurement
                    {
                        PacketLossPercent = 0,
                        RoundTripTimeMs = 10,
                        JitterMs = 1,
                    },
                }));
        Assert.Equal(NetworkPathProfileCodes.ExpectedNextHopMismatch, finding.Code);
    }

    [Fact]
    public void Ac6NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/tool/ping"));
    }

    [Fact]
    public async Task Ac7PersistenceRoundTripStoresBoundProbeOnTrace()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("wan1")],
            rules: [Rule(0, RoutingRuleActions.Lookup, src: "10.1.0.0/24", table: "wan1")],
            staticRoutes:
            [
                Route("0.0.0.0/0", "1.1.1.1", "wan1"),
                Route("0.0.0.0/0", "2.2.2.2", "main"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("0.0.0.0/0", "1.1.1.1", "wan1", immediateGw: "1.1.1.1%ether1"),
            Obs("0.0.0.0/0", "2.2.2.2", "main", immediateGw: "2.2.2.2%ether2"),
        ]);

        NetworkPathProfile profile = Profile(
            destination: "203.0.113.10",
            routingTable: "main",
            sourceInterface: "ether2");

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock);
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                TraceQueries =
                [
                    new RouteResolutionQuery
                    {
                        Family = "ipv4",
                        SourceAddress = "10.1.0.5",
                        DestinationAddress = "203.0.113.10",
                    },
                ],
                NetworkPathProfiles = [profile],
            });
        Assert.True(written.IsSuccess);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);
        NetworkPathProbeBinding binding = Assert.Single(trace.NetworkPathProbeBindings);
        Assert.Equal("wan1", binding.Probe.RoutingTable);
        Assert.Equal("ether1", binding.Probe.Interface);
        Assert.Equal("203.0.113.10", binding.Probe.Destination);
        Assert.NotEmpty(binding.PathFingerprint.ToDigest());
    }

    [Fact]
    public void Ac8PathFingerprintHelperDetectsPrefixNextHopAndEgressChanges()
    {
        RouteResolutionTrace baselineTrace = SampleTrace(
            destination: "203.0.113.10",
            table: "main",
            egress: "ether1",
            gateway: "1.1.1.1",
            prefix: "0.0.0.0/0");
        RoutePathFingerprint baseline = RoutePathFingerprint.FromTrace(baselineTrace);

        RouteResolutionTrace changedGateway = SampleTrace(
            destination: "203.0.113.10",
            table: "main",
            egress: "ether1",
            gateway: "2.2.2.2",
            prefix: "0.0.0.0/0");
        Assert.True(RoutePathFingerprint.PathChanged(baseline, RoutePathFingerprint.FromTrace(changedGateway)));

        RouteResolutionTrace changedEgress = SampleTrace(
            destination: "203.0.113.10",
            table: "main",
            egress: "ether2",
            gateway: "1.1.1.1",
            prefix: "0.0.0.0/0");
        Assert.True(RoutePathFingerprint.PathChanged(baseline, RoutePathFingerprint.FromTrace(changedEgress)));

        RouteResolutionTrace unchanged = SampleTrace(
            destination: "203.0.113.10",
            table: "main",
            egress: "ether1",
            gateway: "1.1.1.1",
            prefix: "0.0.0.0/0");
        Assert.False(RoutePathFingerprint.PathChanged(baseline, RoutePathFingerprint.FromTrace(unchanged)));
        Assert.Equal(baseline, RoutePathFingerprint.FromTrace(unchanged));
    }

    private static RouteResolutionTrace SampleTrace(
        string destination,
        string table,
        string egress,
        string gateway,
        string prefix,
        string? executionPath = null)
        => new()
        {
            Family = "ipv4",
            SourceAddress = "10.1.0.5",
            DestinationAddress = destination,
            SelectedTable = table,
            SelectedVrf = table,
            MatchedPrefix = prefix,
            ImmediateNextHops = [new ImmediateNextHop { Gateway = gateway, Interface = egress }],
            EgressInterfaces = [egress],
            ExecutionPath = executionPath ?? RouteResolutionExecutionPaths.Cpu,
            Decision = RouteResolutionDecisions.Forward,
        };

    private static NetworkPathProfile Profile(
        string destination,
        string? routingTable = null,
        string? vrf = null,
        string? sourceInterface = null,
        string? expectedRoutePrefix = null,
        IReadOnlyList<string>? expectedNextHops = null,
        IReadOnlyList<string>? expectedEgressInterfaces = null,
        string? expectedExecutionPath = null,
        double? maxRtt = null,
        double? maxRegression = null,
        bool critical = false)
        => new()
        {
            SourceDevice = DeviceId.New(),
            Destination = destination,
            RoutingTable = routingTable,
            Vrf = vrf,
            SourceInterface = sourceInterface,
            ExpectedRoutePrefix = expectedRoutePrefix,
            ExpectedNextHops = expectedNextHops ?? [],
            ExpectedEgressInterfaces = expectedEgressInterfaces ?? [],
            ExpectedExecutionPath = expectedExecutionPath,
            MaxRtt = maxRtt,
            MaxRegression = maxRegression,
            Critical = critical,
        };

    private static RouteResolutionTrace Analyze(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
        => RouteResolutionTraceEngine.Analyze(query, configuration, operational);

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static VrfDefinitionFact Vrf(string name, string interfaceName)
        => new() { Name = name, Interfaces = interfaceName, Disabled = "false" };

    private static RoutingRuleFact Rule(
        int ordinal,
        string action,
        string? src = null,
        string? dst = null,
        string? table = null)
        => new()
        {
            EffectiveOrdinal = ordinal,
            Action = action,
            SrcAddress = src,
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

    private static RouteObservationFact Obs(string dst, string gateway, string table, string? immediateGw = null)
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
            RouteType = null,
            Origin = null,
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
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.1", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);
}
