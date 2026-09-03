using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>Living Spec matrix for SEC-02 (#372) — store-backed materializer + observed hash.</summary>
public sealed class DeploymentArtifactMaterializerSec02LivingSpecTests
{
    private static readonly DeviceId Device =
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static readonly Hash256 ProfileHash =
        Hash256.ParseHex("1111111111111111111111111111111111111111111111111111111111111111");

    private static readonly Hash256 SemanticsHash =
        Hash256.ParseHex("2222222222222222222222222222222222222222222222222222222222222222");

    [Fact]
    public void Ac1ReaderRoundTripsCanonicalFilterArtifactBody()
    {
        RouterOsFilterArtifact sealedArtifact = CreateArtifact();
        RouterOsFilterArtifactReader.ParsedBody body =
            RouterOsFilterArtifactReader.Read(sealedArtifact.CanonicalBytes.ToArray());
        RouterOsFilterArtifact resealed = RouterOsFilterArtifact.Create(
            sealedArtifact.CompilerProfileHash,
            sealedArtifact.PhysicalSemanticsHash,
            sealedArtifact.DeviceId,
            body.AddressLists,
            body.Chains,
            body.Anchors,
            body.LayoutVersion);
        Assert.Equal(sealedArtifact.ResourceHash.ToString(), resealed.ResourceHash.ToString());
        Assert.Equal(sealedArtifact.CanonicalBytes.ToArray(), resealed.CanonicalBytes.ToArray());
        Assert.Single(body.AddressLists);
        Assert.Single(body.Chains);
        Assert.Equal("mfc4.a.deadbeefdeadbeef", body.AddressLists[0].Name);
    }

    [Fact]
    public async Task Ac2MaterializerLoadsListsAndChainsFromStore()
    {
        RouterOsFilterArtifact sealedArtifact = CreateArtifact();
        FakeFilterArtifactStore store = new();
        await store.PutIfAbsentAsync(sealedArtifact, Provenance(), CancellationToken.None);
        FilterArtifactStoreDeploymentArtifactMaterializer materializer = new(store);
        DeviceDeploymentPlan plan = Plan(sealedArtifact.ResourceHash);

        DeploymentStagingArtifacts staging = await materializer.LoadAsync(plan);
        Assert.Single(staging.AddressLists);
        Assert.Single(staging.Chains);
        Assert.NotNull(staging.SealedArtifact);
        Assert.Equal(sealedArtifact.ResourceHash.ToString(), staging.SealedArtifact!.ResourceHash.ToString());
    }

    [Fact]
    public async Task Ac3MaterializerFailsClosedWhenArtifactMissing()
    {
        FakeFilterArtifactStore store = new();
        FilterArtifactStoreDeploymentArtifactMaterializer materializer = new(store);
        DeviceDeploymentPlan plan = Plan(Hash256.ParseHex(
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

        DomainInvariantException ex = await Assert.ThrowsAsync<DomainInvariantException>(
            () => materializer.LoadAsync(plan));
        Assert.Contains(DeploymentCodes.ActiveArtifactHashMismatch, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4ObservedHashMatchesSealedWhenLiveStateAligns()
    {
        RouterOsFilterArtifact sealedArtifact = CreateArtifact();
        AddressListArtifact list = sealedArtifact.AddressLists[0];
        ChainArtifact chain = sealedArtifact.Chains[0];
        Dictionary<string, string> props = new(StringComparer.Ordinal)
        {
            ["chain"] = chain.Name,
            ["action"] = chain.Rules[0].Action,
            ["comment"] = chain.Rules[0].Comment,
        };
        foreach ((string key, string value) in chain.Rules[0].Matchers)
        {
            props[key] = value;
        }

        List<ActualAddressListEntry> lists =
        [
            new ActualAddressListEntry(list.Name, list.Entries[0].Address),
        ];
        List<ActualFilterChainRule> rules =
        [
            new ActualFilterChainRule(
                chain.Name,
                chain.Rules[0].Action,
                comment: chain.Rules[0].Comment,
                properties: props),
        ];
        Dictionary<string, string> jumps = new(StringComparer.Ordinal)
        {
            [sealedArtifact.AnchorTargets[0].ExpectedAnchorComment] =
                sealedArtifact.AnchorTargets[0].DesiredJumpTarget,
        };

        Assert.True(ObservedManagedResourceHash.TryCompute(
            sealedArtifact,
            lists,
            rules,
            jumps,
            out Hash256 observed,
            out string? error),
            error);
        Assert.Equal(sealedArtifact.ResourceHash.ToString(), observed.ToString());
    }

    [Fact]
    public void Ac5ObservedHashFailsWhenLiveJumpDiverges()
    {
        RouterOsFilterArtifact sealedArtifact = CreateArtifact();
        AddressListArtifact list = sealedArtifact.AddressLists[0];
        ChainArtifact chain = sealedArtifact.Chains[0];
        Dictionary<string, string> props = new(StringComparer.Ordinal)
        {
            ["chain"] = chain.Name,
            ["action"] = chain.Rules[0].Action,
            ["comment"] = chain.Rules[0].Comment,
        };
        foreach ((string key, string value) in chain.Rules[0].Matchers)
        {
            props[key] = value;
        }

        List<ActualAddressListEntry> lists =
        [
            new ActualAddressListEntry(list.Name, list.Entries[0].Address),
        ];
        List<ActualFilterChainRule> rules =
        [
            new ActualFilterChainRule(
                chain.Name,
                chain.Rules[0].Action,
                comment: chain.Rules[0].Comment,
                properties: props),
        ];
        Dictionary<string, string> jumps = new(StringComparer.Ordinal)
        {
            [sealedArtifact.AnchorTargets[0].ExpectedAnchorComment] = "mfc4.f.r.0000000000000000",
        };

        Assert.True(ObservedManagedResourceHash.TryCompute(
            sealedArtifact,
            lists,
            rules,
            jumps,
            out Hash256 observed,
            out _));
        Assert.NotEqual(sealedArtifact.ResourceHash.ToString(), observed.ToString());
    }

    private static RouterOsFilterArtifact CreateArtifact()
    {
        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, SemanticsHash, Device);
        return RouterOsFilterArtifact.Create(
            ProfileHash,
            SemanticsHash,
            Device,
            addressLists:
            [
                new AddressListArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    Name = "mfc4.a.deadbeefdeadbeef",
                    Entries = [AddressListEntryArtifact.Create("10.10.0.0/16")],
                },
            ],
            chains:
            [
                new ChainArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Forward,
                    Name = "mfc4.f.r." + artifactId,
                    Role = FilterChainArtifactRole.Root,
                    Rules =
                    [
                        FilterRuleArtifact.Create(
                            0,
                            "accept",
                            "mfc:rule:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:0",
                            logicalRuleId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                            matchers: new Dictionary<string, string> { ["connection-state"] = "established,related" }),
                    ],
                },
            ],
            anchorTargets:
            [
                AnchorTargetArtifact.Create(
                    IpAddressFamily.IPv4,
                    FilterBuiltInContext.Forward,
                    "mfc:anchor:v1:4:f",
                    "mfc4.f.r." + artifactId),
            ]);
    }

    private static CompilationProvenance Provenance()
        => new()
        {
            DeviceId = Device,
            LogicalEffectivePolicyHash = Hash256.ParseHex(
                "3333333333333333333333333333333333333333333333333333333333333333"),
            DeviceResolvedPolicyHash = Hash256.ParseHex(
                "4444444444444444444444444444444444444444444444444444444444444444"),
            AnalysisBundleHash = Hash256.ParseHex(
                "5555555555555555555555555555555555555555555555555555555555555555"),
            CapabilityHash = Hash256.ParseHex(
                "6666666666666666666666666666666666666666666666666666666666666666"),
            CompilerProfileHash = ProfileHash,
            CompilerVersion = "test",
            CompiledAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static DeviceDeploymentPlan Plan(Hash256 newHash)
    {
        DeviceDeploymentPlan basePlan = DeploymentTestFactory.DevicePlan(Device, NodeKind.Router);
        return DeviceDeploymentPlan.Create(
            basePlan.DeviceId,
            basePlan.ExpectedRouterOsVersion,
            basePlan.ExpectedCapabilityHash,
            basePlan.ExpectedConfigurationHash,
            basePlan.ExpectedCompatibilityHash,
            basePlan.ExpectedGuardContextHash,
            basePlan.ExpectedAnchorContextHash,
            basePlan.OldArtifactHash,
            basePlan.OldAnchorTargets,
            newHash,
            basePlan.NewAnchorTargets,
            basePlan.AnchorActivationOrder,
            basePlan.AnchorRollbackOrder,
            basePlan.TransitionStateHashes,
            basePlan.RollbackTtl,
            basePlan.Probes);
    }
}
