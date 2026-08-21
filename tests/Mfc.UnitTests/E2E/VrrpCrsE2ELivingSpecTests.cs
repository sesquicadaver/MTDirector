using System.Reflection;
using System.Text.Json;
using Mfc.Application.Deployment;
using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Discovery;
using Mfc.UnitTests.Deployment;
using Mfc.UnitTests.Onboarding;
using Xunit;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOperationState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.UnitTests.E2E;

/// <summary>
/// Living Spec matrix for Issue Set M6-07 AC 1–11 (E2E Workflow Spec VRRP + CRS).
/// Scripted in-process runtimes and fixtures only — live CHR / physical CRS remain OFF.
/// Optional Integration reuse: <c>VrrpVerticalSliceAcceptanceTests</c> (inventory/capture; no live hardware).
/// </summary>
public sealed class VrrpCrsE2ELivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);

    private static readonly DeviceId SwitchDeviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee9"));
    private static readonly Hash256 LogicalHash = Hash256.ParseHex(
        "1111111111111111111111111111111111111111111111111111111111111111");
    private static readonly Hash256 BundleHash = Hash256.ParseHex(
        "2222222222222222222222222222222222222222222222222222222222222222");
    private static readonly Hash256 CapabilityHash = Hash256.ParseHex(
        "3333333333333333333333333333333333333333333333333333333333333333");

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>VRRP active/passive: onboard both members, then coordinated deploy commits.</summary>
    [Fact]
    public async Task Ac1VrrpActivePassiveLifecycleSucceeds()
    {
        DomainNode node = OnboardingTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        OnboardingPlan onboardingPlan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation onboardingOp = OnboardingOperation.Create(onboardingPlan, UserId.New(), T0);
        OnboardingExecutionResult onboarded = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            onboardingPlan,
            onboardingOp,
            [
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(first.Id),
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(second.Id),
            ],
            T0,
            T0);
        Assert.True(onboarded.Succeeded, onboarded.ErrorCode);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
        Assert.Equal(ManagementState.Managed, first.ManagementState);
        Assert.Equal(ManagementState.Managed, second.ManagementState);

        DeploymentPlan deployPlan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(deployPlan, node, UserId.New(), T0);
        ScriptedCluster cluster = new(
            new ScriptedMember(first.Id, VrrpMemberObservedState.Backup),
            new ScriptedMember(second.Id, VrrpMemberObservedState.Master));
        VrrpDeploymentResult deployed = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            deployPlan,
            operation,
            cluster.Members,
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));

        Assert.True(deployed.Succeeded, deployed.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, deployed.State);
        Assert.Contains(deployed.Timeline, static t => t == "precheck:all");
        Assert.Contains(deployed.Timeline, static t => t == "stage:all");
        Assert.Contains(deployed.Timeline, static t => t == "commit:all");
        Assert.All(cluster.Members, static m => Assert.True(m.Prechecked));
        Assert.All(cluster.Members, static m => Assert.True(m.Staged));
        Assert.All(cluster.Members, static m => Assert.True(m.Activated));
        Assert.False(deployed.PartialCommitAttempted);
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Split-master lifecycle is proven fail-closed: detection + deploy block (not simplified).
    /// </summary>
    [Fact]
    public async Task Ac2VrrpSplitMasterLifecycleSucceeds()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        VrrpRoleVector split = new()
        {
            Members =
            [
                RoleSnapshot(a, VrrpMemberObservedState.Master),
                RoleSnapshot(b, VrrpMemberObservedState.Master),
            ],
        };
        Assert.True(VrrpDeploymentPolicy.HasSplitMaster(split));
        DomainInvariantException gate = Assert.Throws<DomainInvariantException>(
            () => VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification(split));
        Assert.StartsWith(DeploymentCodes.VrrpSplitMaster, gate.Message, StringComparison.Ordinal);

        DomainNode node = DeploymentTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedMember m1 = new(first.Id, VrrpMemberObservedState.Master);
        ScriptedMember m2 = new(second.Id, VrrpMemberObservedState.Master);
        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [m1, m2],
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.VrrpSplitMaster, result.ErrorCode);
        Assert.NotEqual(DomainOperationState.Committed, result.State);
        Assert.DoesNotContain(result.Timeline, static t => t == "commit:all");
        Assert.DoesNotContain(result.Timeline, static t => t.StartsWith("activate:", StringComparison.Ordinal));
        Assert.False(result.PartialCommitAttempted);
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>All VRRP members onboard together (same operation; Node MANAGED only when all succeed).</summary>
    [Fact]
    public async Task Ac3AllMembersOnboardTogether()
    {
        DomainNode node = OnboardingTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        Assert.Equal(2, plan.DevicePlans.Count);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(first.Id),
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(second.Id),
            ],
            T0,
            T0);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(2, result.Timeline.Count(static t => t.StartsWith("arm:", StringComparison.Ordinal)));
        Assert.Equal(ManagementState.Managed, first.ManagementState);
        Assert.Equal(ManagementState.Managed, second.ManagementState);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>All VRRP members deploy together (precheck/stage/arm all before any activation; commit:all).</summary>
    [Fact]
    public async Task Ac4AllMembersDeployTogether()
    {
        (VrrpDeploymentResult result, ScriptedCluster cluster) = await HappyPathVrrpAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, result.State);

        int stageAll = result.Timeline.ToList().FindIndex(static t => t == "stage:all");
        int armed = result.Timeline.ToList().FindIndex(static t => t == "watchdog:all-armed");
        int firstActivate = result.Timeline.ToList().FindIndex(static t => t.StartsWith("activate:", StringComparison.Ordinal));
        Assert.True(stageAll >= 0 && armed > stageAll && firstActivate > armed);
        Assert.Contains(result.Timeline, static t => t == "commit:all");
        Assert.Equal(2, cluster.Members.Length);
        Assert.All(cluster.Members, static m => Assert.True(m.Staged));
        Assert.All(cluster.Members, static m => Assert.True(m.Activated));
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Role change after first activation triggers rollback (not silent continue).</summary>
    [Fact]
    public async Task Ac5RoleChangeAfterActivationTriggersRollback()
    {
        DomainNode node = DeploymentTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedMember standby = new(first.Id, VrrpMemberObservedState.Backup);
        ScriptedMember master = new(second.Id, VrrpMemberObservedState.Master)
        {
            FlipRoleAfterFirstPeerActivation = true,
            PeerActivatedSignal = () => standby.Activated,
        };

        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [standby, master],
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.VrrpRoleChangedDuringDeployment, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t == "role-change:detected");
        Assert.NotEmpty(result.RolledBackMembers);
        Assert.DoesNotContain(result.Timeline, static t => t == "commit:all");
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>Partial commit is impossible (policy gate + happy path never sets PartialCommitAttempted).</summary>
    [Fact]
    public async Task Ac6PartialCommitIsImpossible()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => VrrpDeploymentPolicy.EnsureFullCommitAllowed([a, b], new HashSet<DeviceId> { a }));
        Assert.StartsWith(DeploymentCodes.VrrpPartialCommitForbidden, ex.Message, StringComparison.Ordinal);

        (VrrpDeploymentResult happy, _) = await HappyPathVrrpAsync();
        Assert.True(happy.Succeeded, happy.ErrorCode);
        Assert.False(happy.PartialCommitAttempted);
        Assert.Contains(happy.Timeline, static t => t == "commit:all");
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>Physical management addresses are used; VIP-only destination is indeterminate.</summary>
    [Fact]
    public void Ac7PhysicalManagementAddressesAreUsed()
    {
        DomainNode node = DeploymentTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        Assert.Equal("10.0.1.1", first.ManagementEndpoint.Host.Value);
        Assert.Equal("10.0.1.2", second.ManagementEndpoint.Host.Value);
        Assert.NotEqual(first.ManagementEndpoint.Host.Value, second.ManagementEndpoint.Host.Value);

        ManagementAccessProfile physical = ManagementAccessProfile.Create(
            [AddressPrefix.Parse("192.0.2.0/24")],
            "192.0.2.10",
            ManagementPathAnalysis.DefaultApiSslPort,
            physicalManagementAddresses: ["192.0.2.10", "192.0.2.11"],
            virtualManagementAddresses: ["192.0.2.1"]);
        ManagementPathAnalysisResult ok = ManagementPathAnalysis.Analyze(
            physical,
            ManagementIpServiceFacts.Create(found: true, disabled: false, port: "8729", addressPrefixes: null),
            [
                InputGuard(0, dest: "192.0.2.10"),
                OutputGuard(0, source: "192.0.2.10"),
                Anchor("input", 1),
                Anchor("output", 1),
            ]);
        Assert.False(ok.BlocksManagementPath);

        ManagementAccessProfile vipOnly = ManagementAccessProfile.Create(
            [AddressPrefix.Parse("192.0.2.0/24")],
            "192.0.2.1",
            ManagementPathAnalysis.DefaultApiSslPort,
            physicalManagementAddresses: [],
            virtualManagementAddresses: ["192.0.2.1"]);
        ManagementPathAnalysisResult vipBlocked = ManagementPathAnalysis.Analyze(
            vipOnly,
            ManagementIpServiceFacts.Create(found: true, disabled: false, port: "8729", addressPrefixes: null),
            [
                InputGuard(0, dest: "192.0.2.1"),
                OutputGuard(0, source: "192.0.2.1"),
                Anchor("input", 1),
                Anchor("output", 1),
            ]);
        Assert.Contains(
            vipBlocked.Findings,
            static f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);

        ManagementAccessProfile vipAsDest = physical.WithDestination("192.0.2.1");
        ManagementPathAnalysisResult vipDestBlocked = ManagementPathAnalysis.Analyze(
            vipAsDest,
            ManagementIpServiceFacts.Create(found: true, disabled: false, port: "8729", addressPrefixes: null),
            [
                InputGuard(0, dest: "192.0.2.1"),
                OutputGuard(0, source: "192.0.2.1"),
                Anchor("input", 1),
                Anchor("output", 1),
            ]);
        Assert.Contains(
            vipDestBlocked.Findings,
            static f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>CRS INPUT/OUTPUT lifecycle: onboard + deploy without FORWARD anchors.</summary>
    [Fact]
    public async Task Ac8CrsInputOutputLifecycleSucceeds()
    {
        DomainNode node = OnboardingTestFactory.SwitchWithDevice(out DomainDevice device);
        OnboardingPlan onboardingPlan = OnboardingTestFactory.PlanFor(node, T0, includeIpv6: false);
        Assert.False(RequiredAnchorSet.ContainsForward(onboardingPlan.DevicePlans[0].RequiredAnchorSet));
        OnboardingOperation onboardingOp = OnboardingOperation.Create(onboardingPlan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingExecutionResult onboarded = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, onboardingPlan, onboardingOp, [session], T0, T0);
        Assert.True(onboarded.Succeeded, onboarded.ErrorCode);
        Assert.Equal(
            ["enable:mfc:anchor:v1:4:o", "enable:mfc:anchor:v1:4:i"],
            onboarded.Timeline.Where(static t => t.StartsWith("enable:", StringComparison.Ordinal)).ToArray());
        Assert.DoesNotContain(onboarded.Timeline, static t => t.Contains(":4:f", StringComparison.Ordinal));
        Assert.Equal(ManagementState.Managed, node.ManagementState);

        DeploymentPlan deployPlan = DeploymentTestFactory.PlanFor(node, T0);
        Assert.False(RequiredAnchorSet.ContainsForward(
            deployPlan.DevicePlans[0].NewAnchorTargets.Select(static t => t.Key)));
        DeploymentOperation operation = DeploymentOperation.Create(deployPlan, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, deployPlan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(deployPlan, toNew: false);
        StandaloneDeploymentResult deployed = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            deployPlan,
            operation,
            deviceState,
            new FakeRuntime(deployPlan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            deployPlan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);

        Assert.True(deployed.Succeeded, deployed.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, deployed.State);
        Assert.Null(DeploymentPacketPathGate.DescribeBlocker(NodeKind.Switch, []));
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>CRS FORWARD policy is rejected (topology + compiler Switch FORWARD gates).</summary>
    [Fact]
    public void Ac9CrsForwardPolicyIsRejected()
    {
        TopologyDependencyAnalysisResult topology = TopologyDependencyAnalysis.Analyze(
            TopologyDependencyFacts.Create(kind: NodeKind.Switch));
        Assert.Contains(
            topology.Findings,
            static f => f.Code == TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported);
        Assert.True(topology.HasBlockers);

        DeviceFilterCompileRequest forward = new()
        {
            DeviceId = SwitchDeviceId,
            LogicalEffectivePolicyHash = LogicalHash,
            AnalysisBundleHash = BundleHash,
            CapabilityHash = CapabilityHash,
            CompilerProfileHash = RouterOsCompilerProfile.LayoutV1Hash,
            AnalysisPassed = true,
            InputApproved = true,
            AnalysisContextCurrent = true,
            CapabilityCurrent = true,
            CompilerProfileSupported = true,
            NodeKind = NodeKind.Switch,
            ActiveRules =
            [
                PolicyRule.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    PolicyPipelineStage.CompanyAllow,
                    ordinal: 0,
                    TrafficPredicate.Create(),
                    RuleEffectSpec.Create(PolicyRuleEffect.Accept)),
            ],
            ChainContracts = ChainContractSet.CreateForCompanyBaseline(
                [
                    ChainContract.Create(
                        IpAddressFamily.IPv4,
                        PolicyFilterChain.Forward,
                        ChainDefaultDisposition.Drop,
                        rejectMode: null,
                        PolicyRuntimeMode.ManagedOnly),
                ],
                PolicyRuntimeMode.ManagedOnly),
            Addresses = new Dictionary<AddressObjectId, AddressObject>(),
            Services = new Dictionary<ServiceObjectId, ServiceObject>(),
            Zones = new ZoneServiceCompileContext
            {
                DeviceId = SwitchDeviceId,
                Bindings = new Dictionary<ZoneId, NodeZoneBinding>(),
                Observation = new ZoneResolveDeviceObservation
                {
                    DeviceId = SwitchDeviceId,
                    ObservationAvailable = true,
                    Interfaces = [new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false }],
                    InterfaceLists = [],
                    InterfaceListMembers = [],
                },
                Services = new Dictionary<ServiceObjectId, ServiceObject>(),
            },
            CompiledAtUtc = T0,
        };

        DeviceFilterCompileResult forbidden = new DeviceFilterCompiler().Compile(forward);
        Assert.False(forbidden.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.SwitchForwardCompilationForbidden, forbidden.Code);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(forbidden.Code!));
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>Bridge / VLAN / hardware offload are never on the deployment write allowlist.</summary>
    [Fact]
    public async Task Ac10BridgeVlanHardwareOffloadAreNotChanged()
    {
        DomainNode node = DeploymentTestFactory.SwitchWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult deployed = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            deviceState,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.True(deployed.Succeeded, deployed.ErrorCode);

        string[] written = channel.Sent
            .Select(static s => DeploymentWritePaths.Fixed(s.Path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.DoesNotContain(
            written,
            static p => p.Contains("/bridge", StringComparison.OrdinalIgnoreCase)
                        || p.Contains("/vlan", StringComparison.OrdinalIgnoreCase)
                        || p.Contains("hw-offload", StringComparison.OrdinalIgnoreCase)
                        || p.Contains("l3-hw", StringComparison.OrdinalIgnoreCase)
                        || p.Contains("/interface/ethernet/switch", StringComparison.OrdinalIgnoreCase));

        foreach (DeploymentWritePath path in Enum.GetValues<DeploymentWritePath>())
        {
            string fixedPath = DeploymentWritePaths.Fixed(path);
            Assert.False(
                fixedPath.Contains("/bridge", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/vlan", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("hw-offload", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/interface/ethernet/switch", StringComparison.OrdinalIgnoreCase),
                fixedPath);
        }

        Assert.DoesNotContain(
            Enum.GetNames<DeploymentWritePath>(),
            static n => n.Contains("Bridge", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Vlan", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Offload", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Switch", StringComparison.OrdinalIgnoreCase));
    }

    // ── AC 11 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scripted physical CRS hardware fixture contract (lab harness + sanitized profile).
    /// Live physical CRS / CHR remain OFF — DoD is deterministic fixture + Switch FORWARD gate.
    /// </summary>
    [Fact]
    public void Ac11PhysicalCrsHardwareFixtureSucceeds()
    {
        string root = FindRepoRoot();
        string topologyPath = Path.Combine(root, "testlab", "chr", "topologies", "crs-switch", "topology.json");
        string rscFixturePath = Path.Combine(root, "testlab", "chr", "fixtures", "crs-switch-minimal.rsc.example");
        string sanitizedPath = Path.Combine(
            root,
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "bridge-switch-crs.sanitized.json");

        Assert.True(File.Exists(topologyPath), topologyPath);
        Assert.True(File.Exists(rscFixturePath), rscFixturePath);
        Assert.True(File.Exists(sanitizedPath), sanitizedPath);

        using JsonDocument topology = JsonDocument.Parse(File.ReadAllText(topologyPath));
        Assert.Equal("crs-switch", topology.RootElement.GetProperty("id").GetString());
        Assert.Equal("crs", topology.RootElement.GetProperty("management").GetProperty("boardClass").GetString());
        Assert.Equal(
            "fixtures/crs-switch-minimal.rsc.example",
            topology.RootElement.GetProperty("fixture").GetString());
        Assert.Equal("10.255.60.10", topology.RootElement.GetProperty("management").GetProperty("deviceAddress").GetString());

        using JsonDocument sanitized = JsonDocument.Parse(File.ReadAllText(sanitizedPath));
        Assert.Equal("crs", sanitized.RootElement.GetProperty("boardClass").GetString());
        Assert.False(sanitized.RootElement.GetProperty("assumesHardwareSwitchedTrafficPassesIpFirewall").GetBoolean());
        Assert.False(sanitized.RootElement.GetProperty("grantsSwitchWriteCapability").GetBoolean());
        Assert.False(sanitized.RootElement.GetProperty("compilesTransitAcl").GetBoolean());
        Assert.Contains(
            sanitized.RootElement.GetProperty("pathRoleIndicators").EnumerateArray().Select(static e => e.GetString()),
            static s => s == "HardwareOffloadObserved");
        Assert.Contains(
            sanitized.RootElement.GetProperty("pathRoleIndicators").EnumerateArray().Select(static e => e.GetString()),
            static s => s == "L3HardwareOffloadConfigured");

        CapabilityEvaluationResult crsProfile = CapabilityProfileEvaluator.Evaluate(CrsDiscoveryFixture());
        Assert.Equal(BoardClass.Crs, crsProfile.BoardClass);
        Assert.Equal(SupportState.Supported, crsProfile.Profile.SupportState);

        // Exercise Switch FORWARD gate on the CRS management-plane profile (scripted DoD).
        TopologyDependencyAnalysisResult switchGate = TopologyDependencyAnalysis.Analyze(
            TopologyDependencyFacts.Create(kind: NodeKind.Switch));
        Assert.Contains(
            switchGate.Findings,
            static f => f.Code == TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported);

        Assert.Null(typeof(ExecuteStandaloneDeploymentUseCase).GetMethod(
            "DisableHardwareOffload",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static async Task<(VrrpDeploymentResult Result, ScriptedCluster Cluster)> HappyPathVrrpAsync()
    {
        DomainNode node = DeploymentTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedCluster cluster = new(
            new ScriptedMember(first.Id, VrrpMemberObservedState.Backup),
            new ScriptedMember(second.Id, VrrpMemberObservedState.Master));
        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            cluster.Members,
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));
        return (result, cluster);
    }

    private static VrrpMemberRoleSnapshot RoleSnapshot(DeviceId deviceId, VrrpMemberObservedState state)
        => new()
        {
            DeviceId = deviceId,
            HasIndependentRoutedTraffic = false,
            Reachable = true,
            Instances =
            [
                new VrrpInstanceRoleFact
                {
                    Family = IpAddressFamily.IPv4,
                    Vrid = 1,
                    ObservedState = state,
                },
            ],
        };

    private static ActualFilterRule InputGuard(int ordinal, string dest = "192.0.2.10")
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "input",
            ordinal,
            "accept",
            comment: "fwc:guard:api-ssl",
            knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["protocol"] = "tcp",
                ["src-address"] = "192.0.2.0/24",
                ["dst-address"] = dest,
                ["dst-port"] = "8729",
                ["connection-state"] = "new,established",
            });

    private static ActualFilterRule OutputGuard(int ordinal, string source = "192.0.2.10")
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "output",
            ordinal,
            "accept",
            comment: "fwc:guard:api-ssl",
            knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["protocol"] = "tcp",
                ["src-address"] = source,
                ["src-port"] = "8729",
                ["dst-address"] = "192.0.2.0/24",
                ["connection-state"] = "established,related",
            });

    private static ActualFilterRule Anchor(string chain, int ordinal)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            chain,
            ordinal,
            "jump",
            jumpTarget: $"fwc.{chain}.rev1",
            comment: $"fwc:anchor:ipv4:{chain}");

    private static SystemServiceDiscoveryResult CrsDiscoveryFixture()
        => new()
        {
            Identity = new SystemIdentityDiscovery
            {
                Name = "crs-lab",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Resource = new SystemResourceDiscovery
            {
                Version = "7.16.2",
                BuildTime = "2024-01-01 00:00:00",
                ArchitectureName = "arm64",
                BoardName = "CRS326-24G-2S+",
                Platform = "MikroTik",
                Uptime = "1h",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["free-memory"] = "123",
                },
            },
            Routerboard = new SystemRouterboardDiscovery
            {
                Available = true,
                Routerboard = "true",
                Model = "CRS326-24G-2S+",
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
                    Id = null,
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
                Date = "2026-08-21",
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

    private static string FindRepoRoot()
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
