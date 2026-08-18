using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

/// <summary>Living Spec AC rows for M3-07 per-device compile orchestration.</summary>
public sealed class DeviceFilterCompilerTests
{
    private static readonly DeviceId DeviceA = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee1"));
    private static readonly DeviceId DeviceB = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee2"));
    private static readonly Hash256 Logical = Hash256.ParseHex(
        "1111111111111111111111111111111111111111111111111111111111111111");
    private static readonly Hash256 Bundle = Hash256.ParseHex(
        "2222222222222222222222222222222222222222222222222222222222222222");
    private static readonly Hash256 Capability = Hash256.ParseHex(
        "3333333333333333333333333333333333333333333333333333333333333333");

    [Fact]
    public void Ac1CompilerRequiresApprovedPassAnalysis()
    {
        DeviceFilterCompileResult unapproved = new DeviceFilterCompiler().Compile(Request(inputApproved: false));
        Assert.False(unapproved.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, unapproved.Code);

        DeviceFilterCompileResult failed = new DeviceFilterCompiler().Compile(Request(analysisPassed: false));
        Assert.False(failed.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, failed.Code);

        DeviceFilterCompileResult ok = new DeviceFilterCompiler().Compile(Request());
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public void Ac2LogicalEffectiveHashSharedAcrossVrrpMembers()
    {
        NodeFilterCompileResult node = new DeviceFilterCompiler().CompileNode(
        [
            Request(deviceId: DeviceA),
            Request(deviceId: DeviceB),
        ]);
        Assert.True(node.IsSuccess);
        Assert.Equal(Logical.ToString(), node.LogicalEffectivePolicyHash!.ToString());
        Assert.All(node.Devices, d =>
            Assert.Equal(Logical.ToString(), d.Provenance!.LogicalEffectivePolicyHash.ToString()));
    }

    [Fact]
    public void Ac3DeviceResolvedHashIncludesPhysicalZoneResolution()
    {
        ZoneId lan = ZoneId.New();
        DeviceFilterCompileResult left = new DeviceFilterCompiler().Compile(Request(
            deviceId: DeviceA,
            binding: Binding(lan, NodeZoneBindingKind.InterfaceList, ["LAN"], ["ether1"]),
            observation: Observation(["ether1"], List("LAN"), Member("LAN", "ether1"))));
        DeviceFilterCompileResult right = new DeviceFilterCompiler().Compile(Request(
            deviceId: DeviceA,
            binding: Binding(lan, NodeZoneBindingKind.InterfaceList, ["LAN"], ["ether2"]),
            observation: Observation(["ether2"], List("LAN"), Member("LAN", "ether2"))));
        Assert.True(left.IsSuccess);
        Assert.True(right.IsSuccess);
        Assert.NotEqual(
            left.Provenance!.DeviceResolvedPolicyHash.ToString(),
            right.Provenance!.DeviceResolvedPolicyHash.ToString());
    }

    [Fact]
    public void Ac4VrrpRoleIsNotAnInput()
    {
        // Compile request has no VRRP role field; identical zone/capability inputs stay identical.
        DeviceFilterCompileResult first = new DeviceFilterCompiler().Compile(Request());
        DeviceFilterCompileResult second = new DeviceFilterCompiler().Compile(Request());
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Artifact!.ResourceHash.ToString(), second.Artifact!.ResourceHash.ToString());
        Assert.Equal(
            first.Provenance!.DeviceResolvedPolicyHash.ToString(),
            second.Provenance!.DeviceResolvedPolicyHash.ToString());
    }

    [Fact]
    public void Ac5ActiveWanDoesNotAffectArtifact()
    {
        ZoneId wan = ZoneId.New();
        NodeZoneBinding binding = Binding(wan, NodeZoneBindingKind.InterfaceList, ["WAN"], ["ether1"]);
        ZoneResolveDeviceObservation observation = Observation(
            ["ether1"],
            List("WAN"),
            Member("WAN", "ether1"));
        DeviceFilterCompileResult withoutWan = new DeviceFilterCompiler().Compile(Request(
            binding: binding,
            observation: observation,
            activeWanName: null));
        DeviceFilterCompileResult withWan = new DeviceFilterCompiler().Compile(Request(
            binding: binding,
            observation: observation,
            activeWanName: "ether1"));
        Assert.True(withoutWan.IsSuccess);
        Assert.True(withWan.IsSuccess);
        Assert.Equal(withoutWan.Artifact!.ResourceHash.ToString(), withWan.Artifact!.ResourceHash.ToString());
        Assert.Equal(
            withoutWan.Provenance!.DeviceResolvedPolicyHash.ToString(),
            withWan.Provenance!.DeviceResolvedPolicyHash.ToString());
    }

    [Fact]
    public void Ac6Ac7ResourceHashIsContentAddressedAndStable()
    {
        DeviceFilterCompileResult first = new DeviceFilterCompiler().Compile(Request());
        DeviceFilterCompileResult second = new DeviceFilterCompiler().Compile(Request());
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Artifact!.ResourceHash.ToString(), second.Artifact!.ResourceHash.ToString());
        Assert.Equal(first.Summary!.ResourceHash.ToString(), first.Artifact.ResourceHash.ToString());
        Assert.DoesNotContain("add", first.Summary.ArtifactId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/ip/firewall", first.Summary.ArtifactId, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac8PartialNodeCompileIsNotSuccess()
    {
        NodeFilterCompileResult node = new DeviceFilterCompiler().CompileNode(
        [
            Request(deviceId: DeviceA),
            Request(deviceId: DeviceB, inputApproved: false),
        ]);
        Assert.False(node.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, node.Code);
        Assert.Empty(node.Summaries);
        Assert.Empty(node.Devices);
    }

    [Fact]
    public void Ac9SummaryHasNoRouterOsCommands()
    {
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(Request());
        Assert.True(result.IsSuccess);
        FilterArtifactSemanticSummary summary = result.Summary!;
        Assert.Equal(result.Artifact!.ArtifactId, summary.ArtifactId);
        Assert.True(summary.ChainCount > 0);
        Assert.True(summary.RuleCount > 0);
        Assert.DoesNotContain("=", summary.ArtifactId, StringComparison.Ordinal);
        Assert.DoesNotContain(".id", summary.ArtifactId, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac10StaleAnalysisOrCapabilityBlocksCompilation()
    {
        DeviceFilterCompileResult staleAnalysis = new DeviceFilterCompiler().Compile(
            Request(analysisContextCurrent: false));
        Assert.False(staleAnalysis.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerAnalysisStale, staleAnalysis.Code);

        DeviceFilterCompileResult staleCapability = new DeviceFilterCompiler().Compile(
            Request(capabilityCurrent: false));
        Assert.False(staleCapability.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerCapabilityStale, staleCapability.Code);

        DeviceFilterCompileResult unsupportedProfile = new DeviceFilterCompiler().Compile(
            Request(compilerProfileSupported: false));
        Assert.False(unsupportedProfile.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerProfileUnsupported, unsupportedProfile.Code);
    }

    [Fact]
    public void UnresolvedZoneBindingBlocksCompilation()
    {
        ZoneId lan = ZoneId.New();
        // Expected hash deliberately mismatches resolved observation → AnalysisStale.
        NodeZoneBinding stale = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            lan,
            NodeZoneBindingKind.InterfaceList,
            ["LAN"],
            Hash256.ParseHex("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(Request(
            binding: stale,
            observation: Observation(["ether1"], List("LAN"), Member("LAN", "ether1"))));
        Assert.False(result.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerAnalysisStale, result.Code);
    }

    private static DeviceFilterCompileRequest Request(
        DeviceId? deviceId = null,
        bool analysisPassed = true,
        bool inputApproved = true,
        bool analysisContextCurrent = true,
        bool capabilityCurrent = true,
        bool compilerProfileSupported = true,
        NodeZoneBinding? binding = null,
        ZoneResolveDeviceObservation? observation = null,
        string? activeWanName = null)
    {
        DeviceId id = deviceId ?? DeviceA;
        ChainContractSet contracts = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);
        PolicyRule allow = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(Guid.Parse("11111111-2222-3333-4444-555555555555")));
        Dictionary<ZoneId, NodeZoneBinding> bindings = [];
        if (binding is not null)
        {
            bindings[binding.ZoneId] = binding;
        }

        return new DeviceFilterCompileRequest
        {
            DeviceId = id,
            LogicalEffectivePolicyHash = Logical,
            AnalysisBundleHash = Bundle,
            CapabilityHash = Capability,
            CompilerProfileHash = RouterOsCompilerProfile.LayoutV1Hash,
            AnalysisPassed = analysisPassed,
            InputApproved = inputApproved,
            AnalysisContextCurrent = analysisContextCurrent,
            CapabilityCurrent = capabilityCurrent,
            CompilerProfileSupported = compilerProfileSupported,
            ActiveRules = [allow],
            ChainContracts = contracts,
            Addresses = new Dictionary<AddressObjectId, AddressObject>(),
            Services = new Dictionary<ServiceObjectId, ServiceObject>(),
            Zones = new ZoneServiceCompileContext
            {
                DeviceId = id,
                Bindings = bindings,
                Observation = observation ?? Observation(["ether1"]),
                Services = new Dictionary<ServiceObjectId, ServiceObject>(),
                ActiveWanName = activeWanName,
            },
            CompiledAtUtc = DateTimeOffset.Parse("2026-08-18T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private static NodeZoneBinding Binding(
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        IReadOnlyList<string> resolvedMembers)
    {
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(kind, values, resolvedMembers);
        return NodeZoneBinding.Create(new NodeId(Guid.NewGuid()), zoneId, kind, values, expected);
    }

    private static ZoneResolveDeviceObservation Observation(
        IReadOnlyList<string> interfaceNames,
        params object[] extras)
    {
        List<InterfaceListSpec> lists = [];
        List<InterfaceListMemberSpec> members = [];
        foreach (object extra in extras)
        {
            switch (extra)
            {
                case InterfaceListSpec list:
                    lists.Add(list);
                    break;
                case InterfaceListMemberSpec member:
                    members.Add(member);
                    break;
            }
        }

        return new ZoneResolveDeviceObservation
        {
            DeviceId = DeviceA,
            ObservationAvailable = true,
            Interfaces = interfaceNames
                .Select(static n => new ZoneResolveInterfaceObservation { Name = n, Dynamic = false })
                .ToArray(),
            InterfaceLists = lists,
            InterfaceListMembers = members,
        };
    }

    private static InterfaceListSpec List(string name)
        => new()
        {
            Name = name,
            Include = [],
            Exclude = [],
        };

    private static InterfaceListMemberSpec Member(string list, string iface)
        => new()
        {
            List = list,
            Interface = iface,
            Disabled = false,
        };
}
