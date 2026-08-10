using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyPipelineV1Tests
{
    [Fact]
    public void OrderedStagesMatchNormativePipelineV1()
    {
        Assert.Equal(PolicyPipelineV1.StageCount, PolicyPipelineV1.OrderedStages.Count);
        Assert.Equal(
            [
                PolicyPipelineStage.ProtectedControlPlane,
                PolicyPipelineStage.MandatoryPreStateDeny,
                PolicyPipelineStage.StatePrelude,
                PolicyPipelineStage.CompanyDenyExemptions,
                PolicyPipelineStage.CompanyDeny,
                PolicyPipelineStage.SiteDenyExemptions,
                PolicyPipelineStage.SiteDeny,
                PolicyPipelineStage.NodeDenyExemptions,
                PolicyPipelineStage.NodeDeny,
                PolicyPipelineStage.CompanyAllow,
                PolicyPipelineStage.SiteAllow,
                PolicyPipelineStage.NodeAllow,
                PolicyPipelineStage.DefaultDisposition,
            ],
            PolicyPipelineV1.OrderedStages);
        Assert.Equal(
            Enumerable.Range(0, PolicyPipelineV1.StageCount),
            PolicyPipelineV1.OrderedStages.Select(PolicyPipelineV1.Ordinal));
    }

    [Fact]
    public void StageOrderIsIdenticalForEveryFamilyAndChainSurface()
    {
        Assert.Equal(6, PolicyPipelineV1.OrderedSurfaces.Count);
        Assert.Equal(
            [
                (IpAddressFamily.IPv4, PolicyFilterChain.Input),
                (IpAddressFamily.IPv4, PolicyFilterChain.Forward),
                (IpAddressFamily.IPv4, PolicyFilterChain.Output),
                (IpAddressFamily.IPv6, PolicyFilterChain.Input),
                (IpAddressFamily.IPv6, PolicyFilterChain.Forward),
                (IpAddressFamily.IPv6, PolicyFilterChain.Output),
            ],
            PolicyPipelineV1.OrderedSurfaces);

        foreach ((IpAddressFamily _, PolicyFilterChain _) in PolicyPipelineV1.OrderedSurfaces)
        {
            Assert.Same(PolicyPipelineV1.OrderedStages, PolicyPipelineV1.OrderedStages);
        }
    }

    [Fact]
    public void RuleStagesExcludeDefaultDisposition()
    {
        Assert.DoesNotContain(PolicyPipelineStage.DefaultDisposition, PolicyPipelineV1.RuleStages);
        Assert.Equal(12, PolicyPipelineV1.RuleStages.Count);
    }

    [Theory]
    [MemberData(nameof(AllowedPlacements))]
    public void AllowedOwnerEffectCombinationsPass(
        PolicyPipelineStage stage,
        PolicyKind kind,
        PolicyOwnerScope scope,
        PolicyRuleEffect effect)
    {
        PolicyPipelineV1.EnsureOwnerEffectAllowed(stage, kind, scope, effect);
        Assert.True(PolicyPipelineV1.IsOwnerEffectAllowed(stage, kind, scope, effect));
    }

    [Theory]
    [MemberData(nameof(ForbiddenPlacements))]
    public void ForbiddenOwnerEffectCombinationsThrow(
        PolicyPipelineStage stage,
        PolicyKind kind,
        PolicyOwnerScope scope,
        PolicyRuleEffect effect)
    {
        Assert.False(PolicyPipelineV1.IsOwnerEffectAllowed(stage, kind, scope, effect));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyPipelineV1.EnsureOwnerEffectAllowed(stage, kind, scope, effect));
    }

    public static TheoryData<PolicyPipelineStage, PolicyKind, PolicyOwnerScope, PolicyRuleEffect> AllowedPlacements()
        => new()
        {
            {
                PolicyPipelineStage.ProtectedControlPlane, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Accept
            },
            {
                PolicyPipelineStage.MandatoryPreStateDeny, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Drop
            },
            {
                PolicyPipelineStage.MandatoryPreStateDeny, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Reject
            },
            {
                PolicyPipelineStage.StatePrelude, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Accept
            },
            {
                PolicyPipelineStage.StatePrelude, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Drop
            },
            {
                PolicyPipelineStage.StatePrelude, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.FasttrackAccept
            },
            {
                PolicyPipelineStage.CompanyDenyExemptions, PolicyKind.Exception,
                PolicyOwnerScope.Site, PolicyRuleEffect.ExemptDenyStage
            },
            {
                PolicyPipelineStage.CompanyDenyExemptions, PolicyKind.Exception,
                PolicyOwnerScope.Node, PolicyRuleEffect.ExemptDenyStage
            },
            {
                PolicyPipelineStage.CompanyDeny, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Drop
            },
            {
                PolicyPipelineStage.SiteDenyExemptions, PolicyKind.Exception,
                PolicyOwnerScope.Site, PolicyRuleEffect.ExemptDenyStage
            },
            {
                PolicyPipelineStage.SiteDeny, PolicyKind.SiteOverlay,
                PolicyOwnerScope.Site, PolicyRuleEffect.Reject
            },
            {
                PolicyPipelineStage.NodeDenyExemptions, PolicyKind.Exception,
                PolicyOwnerScope.Node, PolicyRuleEffect.ExemptDenyStage
            },
            {
                PolicyPipelineStage.NodeDeny, PolicyKind.NodeOverlay,
                PolicyOwnerScope.Node, PolicyRuleEffect.Drop
            },
            {
                PolicyPipelineStage.CompanyAllow, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, PolicyRuleEffect.Accept
            },
            {
                PolicyPipelineStage.SiteAllow, PolicyKind.SiteOverlay,
                PolicyOwnerScope.Site, PolicyRuleEffect.Accept
            },
            {
                PolicyPipelineStage.NodeAllow, PolicyKind.NodeOverlay,
                PolicyOwnerScope.Node, PolicyRuleEffect.Accept
            },
        };

    public static TheoryData<PolicyPipelineStage, PolicyKind, PolicyOwnerScope, PolicyRuleEffect> ForbiddenPlacements()
    {
        TheoryData<PolicyPipelineStage, PolicyKind, PolicyOwnerScope, PolicyRuleEffect> data = new();

        // Site/Node must not place rules into company stages.
        data.Add(
            PolicyPipelineStage.CompanyDeny, PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site, PolicyRuleEffect.Drop);
        data.Add(
            PolicyPipelineStage.CompanyAllow, PolicyKind.NodeOverlay,
            PolicyOwnerScope.Node, PolicyRuleEffect.Accept);
        data.Add(
            PolicyPipelineStage.ProtectedControlPlane, PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site, PolicyRuleEffect.Accept);

        // Allow stages reject deny effects.
        data.Add(
            PolicyPipelineStage.SiteAllow, PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site, PolicyRuleEffect.Drop);
        data.Add(
            PolicyPipelineStage.NodeAllow, PolicyKind.NodeOverlay,
            PolicyOwnerScope.Node, PolicyRuleEffect.Reject);

        // Deny stages reject ACCEPT / EXEMPT.
        data.Add(
            PolicyPipelineStage.SiteDeny, PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site, PolicyRuleEffect.Accept);
        data.Add(
            PolicyPipelineStage.CompanyDeny, PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company, PolicyRuleEffect.ExemptDenyStage);

        // Exemption stages are Exception-only with EXEMPT_DENY_STAGE.
        data.Add(
            PolicyPipelineStage.CompanyDenyExemptions, PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company, PolicyRuleEffect.ExemptDenyStage);
        data.Add(
            PolicyPipelineStage.SiteDenyExemptions, PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site, PolicyRuleEffect.ExemptDenyStage);
        data.Add(
            PolicyPipelineStage.CompanyDenyExemptions, PolicyKind.Exception,
            PolicyOwnerScope.Site, PolicyRuleEffect.Accept);

        // Wrong owner scope for overlay stage.
        data.Add(
            PolicyPipelineStage.SiteDeny, PolicyKind.SiteOverlay,
            PolicyOwnerScope.Company, PolicyRuleEffect.Drop);
        data.Add(
            PolicyPipelineStage.NodeAllow, PolicyKind.NodeOverlay,
            PolicyOwnerScope.Site, PolicyRuleEffect.Accept);

        // DEFAULT_DISPOSITION never hosts rules.
        foreach (PolicyRuleEffect effect in Enum.GetValues<PolicyRuleEffect>())
        {
            data.Add(
                PolicyPipelineStage.DefaultDisposition, PolicyKind.CompanyBaseline,
                PolicyOwnerScope.Company, effect);
        }

        // Exception cannot place allow/deny rules outside exemption stages.
        data.Add(
            PolicyPipelineStage.CompanyAllow, PolicyKind.Exception,
            PolicyOwnerScope.Site, PolicyRuleEffect.Accept);
        data.Add(
            PolicyPipelineStage.NodeDeny, PolicyKind.Exception,
            PolicyOwnerScope.Node, PolicyRuleEffect.Drop);

        return data;
    }
}
