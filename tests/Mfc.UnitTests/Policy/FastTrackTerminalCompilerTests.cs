using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class FastTrackTerminalCompilerTests
{
    private static readonly DeviceId Device = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static readonly Guid RuleGuid = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private static readonly ServiceObject Tcp = ServiceObject.Create(
        PolicyObjectOwnerScope.Company,
        null,
        null,
        NonEmptyName.Create("tcp-ft"),
        [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);

    private static readonly Dictionary<ServiceObjectId, ServiceObject> Catalog = new()
    {
        [Tcp.Id] = Tcp,
    };

    [Fact]
    public void Ac1OneLogicalVariantCreatesExactlyTwoRules()
    {
        FilterRuleCompileResult result = CompileAllowed();
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Rules.Count);
        Assert.All(result.Rules, r => Assert.Equal(RuleGuid, r.LogicalRuleId));
        Assert.All(result.Rules, r => Assert.Equal(0u, r.VariantIndex));
    }

    [Fact]
    public void Ac2FastTrackAndAcceptAreAdjacent()
    {
        FilterRuleCompileResult result = CompileAllowed();
        Assert.True(result.IsSuccess);
        Assert.Equal("fasttrack-connection", result.Rules[0].Action);
        Assert.Equal("accept", result.Rules[1].Action);
        Assert.Equal(0u, result.Rules[0].Ordinal);
        Assert.Equal(1u, result.Rules[1].Ordinal);
    }

    [Fact]
    public void Ac3PairMatchersAreIdentical()
    {
        FilterRuleCompileResult result = CompileAllowed();
        Assert.True(result.IsSuccess);
        Assert.Equal(result.Rules[0].Matchers, result.Rules[1].Matchers);
        Assert.Equal("6", result.Rules[0].Matchers["protocol"]);
        Assert.Equal("established,related", result.Rules[0].Matchers["connection-state"]);
    }

    [Fact]
    public void Ac4HwOffloadIsNo()
    {
        FilterRuleCompileResult result = CompileAllowed();
        Assert.True(result.IsSuccess);
        Assert.Equal("no", result.Rules[0].ActionParameters["hw-offload"]);
        Assert.DoesNotContain(result.Rules[1].ActionParameters.Keys, static k => k == "hw-offload");
    }

    [Fact]
    public void Ac5FastTrackLoggingIsForbidden()
    {
        PolicyRule rule = AllowedRule(logging: LogSpecification.Create(true, "ft"));
        FilterRuleCompileResult result = Compile(rule, FastTrackTopologyContext.SafeSingleWan);
        Assert.False(result.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.FasttrackLoggingUnsupported, result.Code);
        Assert.Empty(result.Rules);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(result.Code!));
    }

    [Fact]
    public void Ac6PairCommentsHaveFtAndAcSuffixes()
    {
        FilterRuleCompileResult result = CompileAllowed();
        Assert.True(result.IsSuccess);
        Assert.Equal(CompilerComments.FastTrack(RuleGuid, 0), result.Rules[0].Comment);
        Assert.Equal(CompilerComments.FastTrackAccept(RuleGuid, 0), result.Rules[1].Comment);
        Assert.EndsWith(":ft", result.Rules[0].Comment, StringComparison.Ordinal);
        Assert.EndsWith(":ac", result.Rules[1].Comment, StringComparison.Ordinal);
        Assert.False(result.Rules[0].Log);
        Assert.False(result.Rules[1].Log);
    }

    [Fact]
    public void Ac7ChainTerminalMatchesContract()
    {
        ChainContract drop = ChainContract.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            ChainDefaultDisposition.Drop,
            rejectMode: null,
            PolicyRuntimeMode.ManagedOnly);
        FilterRuleArtifact dropTerminal = ChainTerminalCompiler.Compile(drop);
        Assert.Equal("drop", dropTerminal.Action);
        Assert.Equal(CompilerComments.Terminal, dropTerminal.Comment);
        Assert.Equal("terminal", dropTerminal.StructuralRole);
        Assert.Empty(dropTerminal.ActionParameters);

        ChainContract reject = ChainContract.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Input,
            ChainDefaultDisposition.Reject,
            RejectMode.AdminProhibited,
            PolicyRuntimeMode.ManagedOnly);
        FilterRuleArtifact rejectTerminal = ChainTerminalCompiler.Compile(reject);
        Assert.Equal("reject", rejectTerminal.Action);
        Assert.Equal("icmp-admin-prohibited", rejectTerminal.ActionParameters["reject-with"]);
        Assert.Equal(CompilerComments.Terminal, rejectTerminal.Comment);
    }

    [Fact]
    public void Ac8ReturnToUnmanagedCompilesAsExplicitReturn()
    {
        ChainContract contract = ChainContract.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            ChainDefaultDisposition.ReturnToUnmanaged,
            rejectMode: null,
            PolicyRuntimeMode.MigrationCoexistence);
        FilterRuleArtifact terminal = ChainTerminalCompiler.Compile(contract);
        Assert.Equal("return", terminal.Action);
        Assert.Empty(terminal.ActionParameters);
        Assert.Equal(CompilerComments.Terminal, terminal.Comment);
        Assert.Equal("terminal", terminal.StructuralRole);
    }

    [Fact]
    public void Ac9RootChainHasExactlyOneTerminalRule()
    {
        RouterOsFilterArtifact artifact = ManagedChainLayoutBuilder.Build(new ManagedChainLayoutRequest
        {
            CompilerProfileHash = Hash256.ParseHex("1111111111111111111111111111111111111111111111111111111111111111"),
            PhysicalSemanticsHash = Hash256.ParseHex("2222222222222222222222222222222222222222222222222222222222222222"),
            DeviceId = Device,
            Surfaces =
            [
                new ManagedChainSurfacePlan
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Forward,
                    DefaultDisposition = ChainDefaultDisposition.Drop,
                    StatePrelude =
                    [
                        FilterRuleArtifact.Create(0, "accept", "mfc:r:11111111-1111-1111-1111-111111111111:0"),
                    ],
                    CompanyAllow =
                    [
                        FilterRuleArtifact.Create(0, "accept", "mfc:r:22222222-2222-2222-2222-222222222222:0"),
                    ],
                },
            ],
        });

        ChainArtifact root = Assert.Single(artifact.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        FilterRuleArtifact[] terminals = root.Rules
            .Where(static r => r.StructuralRole == "terminal" || r.Comment == CompilerComments.Terminal)
            .ToArray();
        Assert.Single(terminals);
        Assert.Equal(root.Rules[^1], terminals[0]);
        Assert.Equal("drop", terminals[0].Action);
        Assert.Equal(ChainTerminalCompiler.Compile(ChainDefaultDisposition.Drop, null, terminals[0].Ordinal).Action, terminals[0].Action);
        Assert.Equal(ChainTerminalCompiler.Compile(ChainDefaultDisposition.Drop, null, terminals[0].Ordinal).Comment, terminals[0].Comment);
    }

    [Fact]
    public void Ac10UnsupportedFastTrackContextBlocksCompilation()
    {
        FilterRuleCompileResult missingTopology = Compile(AllowedRule(), topology: null);
        Assert.False(missingTopology.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.FasttrackContextUnsupported, missingTopology.Code);
        Assert.Empty(missingTopology.Rules);

        FilterRuleCompileResult ipv6 = Compile(
            AllowedRule(family: IpAddressFamily.IPv6),
            FastTrackTopologyContext.SafeSingleWan);
        Assert.False(ipv6.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.FasttrackContextUnsupported, ipv6.Code);
        Assert.Empty(ipv6.Rules);

        FilterRuleCompileResult capability = Compile(
            AllowedRule(),
            FastTrackTopologyContext.Create(
                DeclaredUplinkMode.One,
                connectionTrackingPresent: false));
        Assert.False(capability.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.FasttrackCapabilityUnsupported, capability.Code);
        Assert.Empty(capability.Rules);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(capability.Code!));
    }

    [Fact]
    public void FastTrackVariantsEmitAdjacentPairsPerVariant()
    {
        ZoneId lan = ZoneId.New();
        NodeZoneBinding binding = Binding(
            lan,
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            ["ether1", "ether2"]);
        PolicyRule rule = AllowedRule(ingress: ZoneSelector.Create([lan]));
        FilterRuleCompileResult result = new FilterMatcherEffectCompiler().Compile(
            [rule],
            Context(FastTrackTopologyContext.SafeSingleWan, binding));
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Rules.Count);
        Assert.Equal("fasttrack-connection", result.Rules[0].Action);
        Assert.Equal("accept", result.Rules[1].Action);
        Assert.Equal("fasttrack-connection", result.Rules[2].Action);
        Assert.Equal("accept", result.Rules[3].Action);
        Assert.Equal(result.Rules[0].Matchers, result.Rules[1].Matchers);
        Assert.Equal(result.Rules[2].Matchers, result.Rules[3].Matchers);
        Assert.Equal(0u, result.Rules[0].VariantIndex);
        Assert.Equal(0u, result.Rules[1].VariantIndex);
        Assert.Equal(1u, result.Rules[2].VariantIndex);
        Assert.Equal(1u, result.Rules[3].VariantIndex);
    }

    private static FilterRuleCompileResult CompileAllowed()
        => Compile(AllowedRule(), FastTrackTopologyContext.SafeSingleWan);

    private static FilterRuleCompileResult Compile(PolicyRule rule, FastTrackTopologyContext? topology)
        => new FilterMatcherEffectCompiler().Compile([rule], Context(topology));

    private static PolicyRule AllowedRule(
        IpAddressFamily family = IpAddressFamily.IPv4,
        ZoneSelector? ingress = null,
        LogSpecification? logging = null)
        => PolicyRule.Create(
            family,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            ordinal: 0,
            TrafficPredicate.Create(
                ingressZones: ingress,
                services: ServiceSelector.Create([Tcp.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: Catalog),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept),
            logging: logging ?? LogSpecification.Disabled,
            id: new RuleId(RuleGuid));

    private static FilterMatcherCompileContext Context(
        FastTrackTopologyContext? topology,
        params NodeZoneBinding[] bindings)
        => new()
        {
            Zones = new ZoneServiceCompileContext
            {
                DeviceId = Device,
                Bindings = bindings.ToDictionary(static b => b.ZoneId),
                Observation = Observation(),
                Services = Catalog,
            },
            Addresses = new Dictionary<AddressObjectId, AddressObject>(),
            FastTrackTopology = topology,
        };

    private static NodeZoneBinding Binding(
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        IReadOnlyList<string> resolvedMembers)
    {
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(kind, values, resolvedMembers);
        return NodeZoneBinding.Create(new NodeId(Guid.NewGuid()), zoneId, kind, values, expected);
    }

    private static ZoneResolveDeviceObservation Observation()
        => new()
        {
            DeviceId = Device,
            ObservationAvailable = true,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = false },
            ],
            InterfaceLists = [],
            InterfaceListMembers = [],
        };
}
