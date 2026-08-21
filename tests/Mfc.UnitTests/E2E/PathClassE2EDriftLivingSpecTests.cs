using System.Reflection;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.UnitTests.Deployment;
using Xunit;
using DomainNode = Mfc.Domain.Inventory.Node;

namespace Mfc.UnitTests.E2E;

/// <summary>
/// Living Spec matrix for Issue Set N1-07 AC 1–12 (next-1 container/VLAN/VETH/HW path classes + drift).
/// Scripted in-process fixtures only — live CHR matrix remains OFF.
/// </summary>
public sealed class PathClassE2EDriftLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 22, 0, 0, TimeSpan.Zero);

    private static Hash256 H(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    private static DriftFinding F(DriftFindingKind kind, string? detail = null)
        => new(kind, detail);

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>Topology graph path class proven: Container/App → VETH → Bridge → VLAN → VRF.</summary>
    [Fact]
    public void Ac1TopologyGraphPathClassesAreProven()
    {
        PacketPathTopologyResult topology = BuildSharedContainerTopology();

        Assert.Contains(
            topology.Edges,
            static e => e.Kind == PacketPathEdgeKind.UsesVeth
                        && e.FromKey == "container:pihole"
                        && e.ToKey == "veth:veth1");
        Assert.Contains(
            topology.Edges,
            static e => e.Kind == PacketPathEdgeKind.UsesVeth
                        && e.FromKey == "app:store"
                        && e.ToKey == "veth:veth2");
        Assert.Contains(
            topology.Edges,
            static e => e.Kind == PacketPathEdgeKind.BridgeMember
                        && e.FromKey == "veth:veth1"
                        && e.ToKey == "bridge:bridge1");
        Assert.Contains(
            topology.Edges,
            static e => e.Kind == PacketPathEdgeKind.BridgeVlanMembership);
        Assert.Contains(
            topology.Edges,
            static e => e.Kind == PacketPathEdgeKind.VlanOnParent
                        && e.FromKey == "vlanif:vlan120");
        Assert.Contains(
            topology.Edges,
            static e => e.Kind == PacketPathEdgeKind.VrfMember
                        && e.ToKey == "vrf:containers");
        Assert.False(topology.AssumesBridgeTrafficPassesIpFirewall);
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>Published container service path (WAN→dstnat→FORWARD→VETH) analyzed; unproven fail-closes.</summary>
    [Fact]
    public void Ac2PublishedContainerServicePathAnalyzedOrFailClosed()
    {
        TopologyDependencyAnalysisResult proven = TopologyDependencyAnalysis.Analyze(
            TopologyDependencyFacts.Create(
                kind: NodeKind.Router,
                natRules:
                [
                    FacilityRuleFact.Create(
                        IpAddressFamily.IPv4,
                        0,
                        "dstnat",
                        "dst-nat",
                        toAddresses: "172.17.0.2",
                        toPorts: "80"),
                ],
                candidate: CandidatePolicySurface.Create(hasDstNatMatcher: true)));
        Assert.DoesNotContain(
            proven.Findings,
            static f => f.Code == TopologyDependencyAnalysisCodes.DstNatMatchWithoutNatEvidence);

        PacketPathAnalysisResult cpu = PacketPathAnalysis.Analyze(
        [
            PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.CpuFirewallPath, bridge: "bridge1", vlanId: "10"),
        ]);
        Assert.False(cpu.BlocksManagedForwardPolicy);
        Assert.False(cpu.HasBlockers);

        PacketPathAnalysisResult unproven = PacketPathAnalysis.Analyze(
        [
            PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.Indeterminate, bridge: "bridge1"),
        ]);
        Assert.True(unproven.BlocksManagedForwardPolicy);
        Assert.Contains(unproven.Findings, static f => f.Code == PacketPathAnalysisCodes.NotProven);

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException blocked = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node,
                plan,
                [],
                T0,
                [
                    PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.HardwareOffloadedPath),
                ]));
        Assert.Contains(PacketPathAnalysisCodes.BypassesIpFirewall, blocked.Message, StringComparison.Ordinal);
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>Container egress path (VETH→bridge/VLAN→FORWARD→srcnat dependency→WAN) is analyzed.</summary>
    [Fact]
    public void Ac3ContainerEgressPathIsAnalyzed()
    {
        TopologyDependencyAnalysisResult egress = TopologyDependencyAnalysis.Analyze(
            TopologyDependencyFacts.Create(
                kind: NodeKind.Router,
                natRules:
                [
                    FacilityRuleFact.Create(
                        IpAddressFamily.IPv4,
                        1,
                        "srcnat",
                        "masquerade"),
                ]));
        Assert.NotNull(egress.TopologyDependencyContextHash);
        Assert.Equal(32, egress.TopologyDependencyContextHash.Bytes.Length);

        PacketPathAnalysisResult path = PacketPathAnalysis.Analyze(
        [
            PacketPathPairFact.Create("veth1", "ether1", PacketPathKind.CpuFirewallPath, bridge: "bridge1", vlanId: "10"),
            PacketPathPairFact.Create("veth1", "ether1", PacketPathKind.MixedPath, bridge: "bridge1"),
        ]);
        Assert.False(path.BlocksManagedForwardPolicy);
        Assert.DoesNotContain(path.Findings, static f => f.Code == PacketPathAnalysisCodes.BypassesIpFirewall);
        Assert.DoesNotContain(path.Findings, static f => f.Code == PacketPathAnalysisCodes.NotProven);

        PacketPathAnalysisResult hwEgress = PacketPathAnalysis.Analyze(
        [
            PacketPathPairFact.Create("veth1", "ether1", PacketPathKind.HardwareOffloadedPath, bridge: "bridge1"),
        ]);
        Assert.True(hwEgress.BlocksManagedForwardPolicy);
        Assert.Contains(hwEgress.Findings, static f => f.Code == PacketPathAnalysisCodes.BypassesIpFirewall);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>Must not assume 1 container=1 VETH / 1 VLAN=1 interface / bridge=firewall.</summary>
    [Fact]
    public void Ac4OneToOneAndBridgeFirewallAssumptionsAreRejected()
    {
        PacketPathTopologyResult topology = BuildSharedContainerTopology();
        Assert.Contains(topology.SharedVethNames, static n => n == "veth1");
        Assert.Contains(
            topology.Findings,
            static f => f.Code == DiscoveryFinding.SharedVethMultiEndpoint);
        Assert.Equal(2, topology.Edges.Count(static e =>
            e.Kind == PacketPathEdgeKind.UsesVeth && e.ToKey == "veth:veth1"));
        Assert.False(topology.AssumesBridgeTrafficPassesIpFirewall);

        // VLAN interface vs bridge VLAN table remain distinct node kinds.
        Assert.Contains(topology.Nodes, static n => n.Kind == PacketPathNodeKind.VlanInterface);
        Assert.Contains(topology.Nodes, static n => n.Kind == PacketPathNodeKind.BridgeVlan);
        Assert.Contains(topology.Nodes, static n => n.Kind == PacketPathNodeKind.Bridge);
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Container running/stopped alone is observation, not configuration drift.</summary>
    [Fact]
    public void Ac5ContainerRunningStateIsObservationNotConfigurationDrift()
    {
        Assert.Equal(DriftSeverity.Observation, DriftClassifier.Classify(DriftFindingKind.ContainerRunningStateChanged));
        Assert.True(PathClassConfigDriftVoiding.IsPathClassObservationKind(DriftFindingKind.ContainerRunningStateChanged));
        Assert.False(PathClassConfigDriftVoiding.IsPathClassConfigurationKind(DriftFindingKind.ContainerRunningStateChanged));

        Hash256 committed = H(11);
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed,
            committed,
            desiredArtifactHash: committed,
            [F(DriftFindingKind.ContainerRunningStateChanged, "running→stopped")]);

        Assert.Equal(DriftOutcome.ObservationOnly, evaluation.Outcome);
        Assert.False(evaluation.ConfigurationDriftPresent);
        Assert.False(evaluation.BlocksDeployment);
        Assert.False(PathClassConfigDriftVoiding.Evaluate(evaluation).VoidsAll);
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// VETH / VLAN / bridge / VRF / NAT exposure / HW path config changes are Critical and void
    /// analysis / approval / artifact readiness / unexecuted plan.
    /// </summary>
    [Theory]
    [InlineData(DriftFindingKind.VethConfigChanged)]
    [InlineData(DriftFindingKind.VlanConfigChanged)]
    [InlineData(DriftFindingKind.BridgeMembershipConfigChanged)]
    [InlineData(DriftFindingKind.VrfAssignmentConfigChanged)]
    [InlineData(DriftFindingKind.ContainerNatExposureConfigChanged)]
    [InlineData(DriftFindingKind.HardwarePathConfigChanged)]
    public void Ac6PathClassConfigChangesAreCriticalAndVoidReadiness(DriftFindingKind kind)
    {
        Assert.Equal(DriftSeverity.Critical, DriftClassifier.Classify(kind));
        Assert.True(PathClassConfigDriftVoiding.IsPathClassConfigurationKind(kind));

        Hash256 committed = H(21);
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed,
            committed,
            desiredArtifactHash: committed,
            [F(kind, "path-class-config")]);

        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);
        Assert.True(evaluation.BlocksDeployment);
        PathClassVoidingResult voiding = PathClassConfigDriftVoiding.Evaluate(evaluation);
        Assert.True(voiding.VoidsStaticAnalysis);
        Assert.True(voiding.VoidsApprovalContext);
        Assert.True(voiding.VoidsCompiledArtifactReadiness);
        Assert.True(voiding.VoidsUnexecutedDeploymentPlan);
        Assert.True(voiding.VoidsAll);

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node,
                plan,
                [],
                T0,
                DeploymentTestFactory.CpuPairs(),
                hasBlockingCriticalDrift: evaluation.BlocksDeployment));
        Assert.Contains(DriftCodes.CriticalDriftBlocksDeploy, ex.Message, StringComparison.Ordinal);
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Active route / VETH running / bridge-port / HW-offload observation fields alone do not create
    /// configuration drift when config hashes match (M6-06 AC10 style).
    /// </summary>
    [Theory]
    [InlineData(DriftFindingKind.ActiveWanChanged)]
    [InlineData(DriftFindingKind.VethRunningStateChanged)]
    [InlineData(DriftFindingKind.BridgePortStateChanged)]
    [InlineData(DriftFindingKind.HardwareOffloadStateChanged)]
    [InlineData(DriftFindingKind.InterfaceRunningStateChanged)]
    public void Ac7PathClassObservationsDoNotCreateConfigurationDrift(DriftFindingKind kind)
    {
        Assert.Equal(DriftSeverity.Observation, DriftClassifier.Classify(kind));
        Assert.True(PathClassConfigDriftVoiding.IsPathClassObservationKind(kind));

        Hash256 committed = H(31);
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed,
            committed,
            desiredArtifactHash: committed,
            [F(kind, "obs-only")]);

        Assert.Equal(DriftOutcome.ObservationOnly, evaluation.Outcome);
        Assert.False(evaluation.ConfigurationDriftPresent);
        Assert.False(evaluation.BlocksDeployment);
        Assert.False(PathClassConfigDriftVoiding.Evaluate(evaluation).VoidsAll);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Controller has no write APIs for containers, Apps, VLAN create, bridge/VLAN table, VRF,
    /// HW offload, or container NAT (ArchitectureBoundary + DeploymentWritePaths + reflection).
    /// </summary>
    [Fact]
    public void Ac8ControllerHasNoPathClassWriteApis()
    {
        string[] forbiddenNamespaces =
        [
            "Mfc.RouterOs.Write",
            "Mfc.RouterOs.Scripting",
            "Mfc.RouterOs.Terminal",
            "Mfc.RouterOs.GenericCommands",
        ];
        Type[] routerOsTypes = typeof(RosReadCommandExecutor).Assembly.GetTypes();
        foreach (string ns in forbiddenNamespaces)
        {
            Assert.DoesNotContain(
                routerOsTypes,
                t => string.Equals(t.Namespace, ns, StringComparison.Ordinal)
                     || (t.Namespace is not null && t.Namespace.StartsWith(ns + ".", StringComparison.Ordinal)));
        }

        foreach (DeploymentWritePath path in Enum.GetValues<DeploymentWritePath>())
        {
            string fixedPath = DeploymentWritePaths.Fixed(path);
            Assert.False(
                fixedPath.Contains("/container", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/app", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/interface/vlan", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/interface/bridge", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/ip/vrf", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/firewall/nat", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("hw-offload", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/interface/ethernet/switch", StringComparison.OrdinalIgnoreCase),
                fixedPath);
        }

        Assert.DoesNotContain(
            Enum.GetNames<DeploymentWritePath>(),
            static n => n.Contains("Container", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Vlan", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Bridge", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Vrf", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Nat", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Offload", StringComparison.OrdinalIgnoreCase));

        Type[] applicationTypes = typeof(Mfc.Application.Deployment.ExecuteStandaloneDeploymentUseCase).Assembly.GetTypes();
        Assert.DoesNotContain(
            applicationTypes,
            static t => t.Name.Contains("CreateContainer", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("CreateVlan", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("SetHardwareOffload", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("CreateContainerNat", StringComparison.OrdinalIgnoreCase));
        Assert.Null(typeof(Mfc.Application.Deployment.ExecuteStandaloneDeploymentUseCase).GetMethod(
            "DisableHardwareOffload",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>Packet-path blockers still fail-close deployment via DeploymentPacketPathGate.</summary>
    [Fact]
    public void Ac9PacketPathBlockersFailCloseDeployment()
    {
        Assert.Equal(
            PacketPathAnalysisCodes.NotProven,
            DeploymentPacketPathGate.DescribeBlocker(NodeKind.Router, []));
        Assert.Equal(
            PacketPathAnalysisCodes.BypassesIpFirewall,
            DeploymentPacketPathGate.DescribeBlocker(
                NodeKind.Router,
                [PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.HardwareOffloadedPath)]));
        Assert.Equal(
            PacketPathAnalysisCodes.NotProven,
            DeploymentPacketPathGate.DescribeBlocker(
                NodeKind.Vrrp,
                [PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.Indeterminate)]));
        Assert.Null(
            DeploymentPacketPathGate.DescribeBlocker(
                NodeKind.Router,
                [
                    PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.CpuFirewallPath),
                    PacketPathPairFact.Create("veth1", "ether1", PacketPathKind.MixedPath),
                ]));
        Assert.Null(DeploymentPacketPathGate.DescribeBlocker(NodeKind.Switch, []));

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        Assert.False(
            DeploymentPacketPathGate.TryAllowStaging(
                operation,
                node.DeclaredKind,
                [PacketPathPairFact.Create("ether1", "veth1", PacketPathKind.Indeterminate)],
                T0.AddSeconds(1)));
        Assert.Equal(DeploymentOperationState.Blocked, operation.State);
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>Zone resolve with container:/app: markers works for path-class endpoints.</summary>
    [Fact]
    public void Ac10ZoneResolveContainerAppMarkersWork()
    {
        NodeZoneBinding containerBinding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["container:pihole"],
            H(41));
        ZoneBindingResolveResult container = ZoneResolveEngine.Resolve(
            containerBinding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = true,
                Interfaces =
                [
                    new ZoneResolveInterfaceObservation { Name = "veth1", Dynamic = false },
                    new ZoneResolveInterfaceObservation { Name = "veth2", Dynamic = false },
                ],
                InterfaceLists = [],
                InterfaceListMembers = [],
                ContainerVethEdges =
                [
                    new ZoneResolveContainerVethEdge
                    {
                        EndpointKind = "container",
                        EndpointName = "pihole",
                        VethName = "veth1",
                    },
                    new ZoneResolveContainerVethEdge
                    {
                        EndpointKind = "container",
                        EndpointName = "pihole",
                        VethName = "veth2",
                    },
                ],
                SharedVethNames = [],
            });
        Assert.Equal(["veth1", "veth2"], container.ResolvedMembers);
        Assert.Empty(container.Blockers);

        NodeZoneBinding appBinding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            ZoneId.New(),
            NodeZoneBindingKind.SingleInterface,
            ["app:store"],
            H(42));
        ZoneBindingResolveResult app = ZoneResolveEngine.Resolve(
            appBinding,
            new ZoneResolveDeviceObservation
            {
                DeviceId = new DeviceId(Guid.NewGuid()),
                ObservationAvailable = true,
                Interfaces = [new ZoneResolveInterfaceObservation { Name = "veth-app", Dynamic = false }],
                InterfaceLists = [],
                InterfaceListMembers = [],
                ContainerVethEdges =
                [
                    new ZoneResolveContainerVethEdge
                    {
                        EndpointKind = "app",
                        EndpointName = "store",
                        VethName = "veth-app",
                    },
                ],
                SharedVethNames = [],
            });
        Assert.Equal(["veth-app"], app.ResolvedMembers);
        Assert.Empty(app.Blockers);
    }

    // ── AC 11 ─────────────────────────────────────────────────────────────────────

    /// <summary>Drift events for path-class Critical findings block new deployment (M6-02 gate).</summary>
    [Fact]
    public void Ac11PathClassCriticalDriftBlocksNewDeployment()
    {
        Hash256 committed = H(51);
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed,
            committed,
            desiredArtifactHash: committed,
            [
                F(DriftFindingKind.VethConfigChanged, "veth1 address"),
                F(DriftFindingKind.HardwarePathConfigChanged, "l3hw"),
            ]);
        Assert.True(evaluation.BlocksDeployment);
        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node,
                plan,
                [],
                T0,
                DeploymentTestFactory.CpuPairs(),
                hasBlockingCriticalDrift: true));
        Assert.Contains(DriftCodes.CriticalDriftBlocksDeploy, ex.Message, StringComparison.Ordinal);

        // Observation-only path-class drift must not set the gate flag.
        DeploymentOperationGate.EnsureCanStart(
            node,
            plan,
            [],
            T0,
            DeploymentTestFactory.CpuPairs(),
            hasBlockingCriticalDrift: false);
    }

    // ── AC 12 ─────────────────────────────────────────────────────────────────────

    /// <summary>Deterministic Living Spec — no live CHR dependency in this suite or docs gate.</summary>
    [Fact]
    public void Ac12DeterministicLivingSpecNoLiveChr()
    {
        string root = FindRepoRoot();
        string thisFile = Path.Combine(root, "tests", "Mfc.UnitTests", "E2E", "PathClassE2EDriftLivingSpecTests.cs");
        Assert.True(File.Exists(thisFile), thisFile);
        string source = File.ReadAllText(thisFile);
        Assert.Contains("live CHR matrix remains OFF", source, StringComparison.Ordinal);
        Assert.Contains("Scripted in-process fixtures only", source, StringComparison.Ordinal);
        // Forbidden live-lab tokens assembled so this assertion body cannot self-match.
        string forbiddenLiveToken = string.Concat("Connect", "ToLive", "Chr");
        Assert.DoesNotContain(forbiddenLiveToken, source, StringComparison.Ordinal);

        string testing = File.ReadAllText(Path.Combine(root, "docs", "development", "testing.md"));
        Assert.Contains("N1-07", testing, StringComparison.Ordinal);
        Assert.Contains("PathClassE2EDriftLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Live CHR", testing, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static PacketPathTopologyResult BuildSharedContainerTopology()
        => PacketPathTopologyDiscovery.BuildResult(
            containers: Ok(
                RosReadCommandId.Containers,
                Row(("name", "pihole"), ("interface", "veth1"), ("status", "running")),
                Row(("name", "pg"), ("interface", "veth1"), ("status", "stopped"))),
            apps: Ok(
                RosReadCommandId.Apps,
                Row(("name", "store"), ("interface", "veth2"), ("running", "true"))),
            vethInterfaces: Ok(
                RosReadCommandId.VethInterfaces,
                Row(("name", "veth1"), ("address", "172.17.0.2/24"), ("gateway", "172.17.0.1"), ("running", "true")),
                Row(("name", "veth2"), ("address", "172.18.0.2/24"), ("running", "true"))),
            vlanInterfaces: Ok(
                RosReadCommandId.VlanInterfaces,
                Row(("name", "vlan120"), ("vlan-id", "120"), ("interface", "bridge1"), ("running", "true"))),
            bridges: Bridges(
                bridgeName: "bridge1",
                ports:
                [
                    ("bridge1", "veth1", "10"),
                    ("bridge1", "ether2", "1"),
                ],
                vlans:
                [
                    ("bridge1", "10", "ether1", "veth1"),
                ]),
            vrfs: Ok(
                RosReadCommandId.IpVrfs,
                Row(("name", "containers"), ("interfaces", "vlan120,veth2"))));

    private static BridgeSwitchDiscoveryResult Bridges(
        string bridgeName,
        (string Bridge, string Interface, string Pvid)[] ports,
        (string Bridge, string VlanIds, string Tagged, string Untagged)[] vlans)
    {
        RosReadCommandResult bridgeRows = Ok(
            RosReadCommandId.Bridges,
            Row(("name", bridgeName), ("vlan-filtering", "true"), ("pvid", "1"), ("running", "true")));
        RosReadCommandResult portRows = Ok(
            RosReadCommandId.BridgePorts,
            ports.Select(p => Row(("bridge", p.Bridge), ("interface", p.Interface), ("pvid", p.Pvid))).ToArray());
        RosReadCommandResult vlanRows = Ok(
            RosReadCommandId.BridgeVlans,
            vlans.Select(v => Row(
                ("bridge", v.Bridge),
                ("vlan-ids", v.VlanIds),
                ("tagged", v.Tagged),
                ("untagged", v.Untagged))).ToArray());
        return BridgeSwitchDiscovery.BuildResult(
            bridgeRows,
            portRows,
            Ok(RosReadCommandId.BridgeSettings, Row(("use-ip-firewall", "false"))),
            vlanRows,
            Ok(RosReadCommandId.EthernetSwitches),
            Ok(RosReadCommandId.EthernetSwitchPorts));
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
