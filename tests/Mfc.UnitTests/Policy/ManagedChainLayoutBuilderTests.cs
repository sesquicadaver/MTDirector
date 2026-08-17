using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ManagedChainLayoutBuilderTests
{
    private static readonly DeviceId Device =
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static readonly Hash256 ProfileHash =
        Hash256.ParseHex("1111111111111111111111111111111111111111111111111111111111111111");

    private static readonly Hash256 SemanticsHash =
        Hash256.ParseHex("2222222222222222222222222222222222222222222222222222222222222222");

    [Fact]
    public void Ac1NamespacesAreMfc4AndMfc6()
    {
        Assert.Equal("mfc4", ManagedChainNamespace.FamilyPrefix(IpAddressFamily.IPv4));
        Assert.Equal("mfc6", ManagedChainNamespace.FamilyPrefix(IpAddressFamily.IPv6));
        Assert.Equal(
            "mfc4.i.r.0123456789abcdef",
            ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                FilterChainArtifactRole.Root,
                "0123456789ABCDEF"));
        Assert.Equal(
            "mfc6.f.dc.0123456789abcdef",
            ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv6,
                FilterBuiltInContext.Forward,
                FilterChainArtifactRole.CompanyDeny,
                "0123456789abcdef"));
        Assert.Equal("mfc4.a.deadbeefdeadbeef", ManagedChainNamespace.AddressListName(IpAddressFamily.IPv4, "DEADBEEFDEADBEEF"));
        Assert.Equal("mfc:anchor:v1:4:f", ManagedChainNamespace.DesiredAnchorComment(IpAddressFamily.IPv4, FilterBuiltInContext.Forward));
    }

    [Fact]
    public void Ac2OneRootPerFamilyChain()
    {
        RouterOsFilterArtifact artifact = Build(
            Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Forward),
            Surface(IpAddressFamily.IPv6, FilterBuiltInContext.Input));

        Assert.Equal(3, artifact.Chains.Count(static c => c.Role == FilterChainArtifactRole.Root));
        Assert.Contains(artifact.Chains, c => c is { Role: FilterChainArtifactRole.Root, Family: IpAddressFamily.IPv4, BuiltInContext: FilterBuiltInContext.Input });
        Assert.Contains(artifact.Chains, c => c is { Role: FilterChainArtifactRole.Root, Family: IpAddressFamily.IPv4, BuiltInContext: FilterBuiltInContext.Forward });
        Assert.Contains(artifact.Chains, c => c is { Role: FilterChainArtifactRole.Root, Family: IpAddressFamily.IPv6, BuiltInContext: FilterBuiltInContext.Input });

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            Build(
                Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
                Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Input)));
        Assert.Contains("one root chain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac3Ac4MaxThreeDenyChainsAndEmptyDenyOmitsChainAndJump()
    {
        RouterOsFilterArtifact emptyDenies = Build(Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Forward));
        ChainArtifact rootOnly = Assert.Single(emptyDenies.Chains);
        Assert.Equal(FilterChainArtifactRole.Root, rootOnly.Role);
        Assert.DoesNotContain(rootOnly.Rules, static r => r.Action == "jump");

        RouterOsFilterArtifact full = Build(new ManagedChainSurfacePlan
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Forward,
            DefaultDisposition = ChainDefaultDisposition.Drop,
            CompanyDenyBody = [Body("drop", "mfc:r:11111111-1111-1111-1111-111111111111:0")],
            SiteDenyBody = [Body("drop", "mfc:r:22222222-2222-2222-2222-222222222222:0")],
            NodeDenyBody = [Body("drop", "mfc:r:33333333-3333-3333-3333-333333333333:0")],
        });

        Assert.Equal(3, full.Chains.Count(static c => c.Role != FilterChainArtifactRole.Root));
        Assert.Contains(full.Chains, static c => c.Role == FilterChainArtifactRole.CompanyDeny);
        Assert.Contains(full.Chains, static c => c.Role == FilterChainArtifactRole.SiteDeny);
        Assert.Contains(full.Chains, static c => c.Role == FilterChainArtifactRole.NodeDeny);

        ChainArtifact root = Assert.Single(full.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        Assert.Equal(3, root.Rules.Count(static r => r.Action == "jump"));
    }

    [Fact]
    public void Ac5RootStageOrderMatchesPipelineV1()
    {
        Guid protectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid allowId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        RouterOsFilterArtifact artifact = Build(new ManagedChainSurfacePlan
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Forward,
            DefaultDisposition = ChainDefaultDisposition.Drop,
            ProtectedControlPlane = [Body("accept", $"mfc:r:{protectId:D}:0", protectId)],
            CompanyDenyBody = [Body("drop", "mfc:r:cccccccc-cccc-cccc-cccc-cccccccccccc:0")],
            SiteDenyBody = [Body("drop", "mfc:r:dddddddd-dddd-dddd-dddd-dddddddddddd:0")],
            CompanyAllow = [Body("accept", $"mfc:r:{allowId:D}:0", allowId)],
        });

        ChainArtifact root = Assert.Single(artifact.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        Assert.Equal(5, root.Rules.Length);
        Assert.Equal($"mfc:r:{protectId:D}:0", root.Rules[0].Comment);
        Assert.Equal(ManagedChainLayoutBuilder.JumpCompanyDenyComment, root.Rules[1].Comment);
        Assert.Equal(ManagedChainLayoutBuilder.JumpSiteDenyComment, root.Rules[2].Comment);
        Assert.Equal($"mfc:r:{allowId:D}:0", root.Rules[3].Comment);
        Assert.Equal(ManagedChainLayoutBuilder.TerminalComment, root.Rules[4].Comment);

        Assert.Equal(
            0,
            PolicyPipelineV1.Ordinal(PolicyPipelineStage.ProtectedControlPlane));
        Assert.True(PolicyPipelineV1.Ordinal(PolicyPipelineStage.CompanyDeny)
                    < PolicyPipelineV1.Ordinal(PolicyPipelineStage.SiteDeny));
        Assert.True(PolicyPipelineV1.Ordinal(PolicyPipelineStage.SiteDeny)
                    < PolicyPipelineV1.Ordinal(PolicyPipelineStage.CompanyAllow));
        Assert.True(PolicyPipelineV1.Ordinal(PolicyPipelineStage.CompanyAllow)
                    < PolicyPipelineV1.Ordinal(PolicyPipelineStage.DefaultDisposition));
    }

    [Fact]
    public void Ac6DenyChainEndsWithUnconditionalReturn()
    {
        RouterOsFilterArtifact artifact = Build(new ManagedChainSurfacePlan
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Input,
            DefaultDisposition = ChainDefaultDisposition.Drop,
            CompanyDenyBody =
            [
                Body("return", "mfc:r:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:0:ex"),
                Body("drop", "mfc:r:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:0"),
            ],
        });

        ChainArtifact deny = Assert.Single(artifact.Chains, static c => c.Role == FilterChainArtifactRole.CompanyDeny);
        Assert.Equal(3, deny.Rules.Length);
        FilterRuleArtifact terminal = deny.Rules[^1];
        Assert.Equal("return", terminal.Action);
        Assert.Equal(ManagedChainLayoutBuilder.ReturnCompanyDenyComment, terminal.Comment);
        Assert.Empty(terminal.Matchers);
        Assert.Empty(terminal.ActionParameters);
    }

    [Fact]
    public void Ac7Ac8RootHasExplicitTerminalAndAcceptImpossible()
    {
        RouterOsFilterArtifact drop = Build(Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Output));
        FilterRuleArtifact dropTerminal = Assert.Single(drop.Chains[0].Rules);
        Assert.Equal("drop", dropTerminal.Action);
        Assert.Equal(ManagedChainLayoutBuilder.TerminalComment, dropTerminal.Comment);

        RouterOsFilterArtifact reject = Build(new ManagedChainSurfacePlan
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Output,
            DefaultDisposition = ChainDefaultDisposition.Reject,
            RejectModeValue = RejectMode.TcpReset,
        });
        FilterRuleArtifact rejectTerminal = Assert.Single(reject.Chains[0].Rules);
        Assert.Equal("reject", rejectTerminal.Action);
        Assert.Equal("tcp-reset", rejectTerminal.ActionParameters["reject-with"]);

        Assert.Throws<DomainInvariantException>(() =>
            Build(new ManagedChainSurfacePlan
            {
                Family = IpAddressFamily.IPv4,
                BuiltInContext = FilterBuiltInContext.Input,
                DefaultDisposition = ChainDefaultDisposition.Reject,
            }));

        Assert.False(Enum.IsDefined(typeof(ChainDefaultDisposition), (byte)255));
        Assert.Equal(3, Enum.GetValues<ChainDefaultDisposition>().Length);
        Assert.DoesNotContain(Enum.GetNames<ChainDefaultDisposition>(), static n => n.Contains("Accept", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac9ManagementGuardRejectedFromArtifact()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            Build(new ManagedChainSurfacePlan
            {
                Family = IpAddressFamily.IPv4,
                BuiltInContext = FilterBuiltInContext.Input,
                DefaultDisposition = ChainDefaultDisposition.Drop,
                ProtectedControlPlane =
                [
                    Body("accept", "mfc:guard:v1:0123456789abcdef:4:i:0"),
                ],
            }));
        Assert.Contains("Management guard", ex.Message, StringComparison.OrdinalIgnoreCase);

        RouterOsFilterArtifact artifact = Build(Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Input));
        Assert.DoesNotContain(artifact.Chains.SelectMany(static c => c.Rules), static r => ActualFilterMarker.IsGuard(r.Comment));
    }

    [Fact]
    public void Ac10CompilerEmitsDesiredTargetNotPhysicalAnchorRules()
    {
        DomainInvariantException tainted = Assert.Throws<DomainInvariantException>(() =>
            Build(new ManagedChainSurfacePlan
            {
                Family = IpAddressFamily.IPv4,
                BuiltInContext = FilterBuiltInContext.Forward,
                DefaultDisposition = ChainDefaultDisposition.Drop,
                StatePrelude =
                [
                    Body("jump", "mfc:anchor:v1:4:f"),
                ],
            }));
        Assert.Contains("Physical anchor", tainted.Message, StringComparison.OrdinalIgnoreCase);

        RouterOsFilterArtifact withTarget = Build(Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Forward));
        Assert.Equal(ManagedChainNamespace.LayoutVersion, withTarget.LayoutVersion);
        AnchorTargetArtifact target = Assert.Single(withTarget.AnchorTargets);
        Assert.Equal("mfc:anchor:v1:4:f", target.ExpectedAnchorComment);
        Assert.Equal(withTarget.Chains.Single(static c => c.Role == FilterChainArtifactRole.Root).Name, target.DesiredJumpTarget);
        Assert.DoesNotContain(
            withTarget.Chains.SelectMany(static c => c.Rules),
            static r => ActualFilterMarker.IsAnchor(r.Comment));

        RouterOsFilterArtifact withoutTarget = ManagedChainLayoutBuilder.Build(new ManagedChainLayoutRequest
        {
            CompilerProfileHash = ProfileHash,
            PhysicalSemanticsHash = SemanticsHash,
            DeviceId = Device,
            EmitDesiredAnchorTargets = false,
            Surfaces = [Surface(IpAddressFamily.IPv4, FilterBuiltInContext.Forward)],
        });
        Assert.Empty(withoutTarget.AnchorTargets);
        Assert.DoesNotContain(
            withoutTarget.Chains.SelectMany(static c => c.Rules),
            static r => ActualFilterMarker.IsAnchor(r.Comment));
    }

    private static RouterOsFilterArtifact Build(params ManagedChainSurfacePlan[] surfaces)
        => ManagedChainLayoutBuilder.Build(new ManagedChainLayoutRequest
        {
            CompilerProfileHash = ProfileHash,
            PhysicalSemanticsHash = SemanticsHash,
            DeviceId = Device,
            Surfaces = surfaces,
        });

    private static ManagedChainSurfacePlan Surface(IpAddressFamily family, FilterBuiltInContext builtIn)
        => new()
        {
            Family = family,
            BuiltInContext = builtIn,
            DefaultDisposition = ChainDefaultDisposition.Drop,
        };

    private static FilterRuleArtifact Body(string action, string comment, Guid? logicalRuleId = null)
        => FilterRuleArtifact.Create(
            ordinal: 0,
            action: action,
            comment: comment,
            logicalRuleId: logicalRuleId);
}
