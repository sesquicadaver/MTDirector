using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Redaction;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class RoutingAssuranceAllowlistTests
{
    private static readonly string[] ExpectedFixedPaths =
    [
        "/routing/table/print",
        "/routing/settings/print",
        "/routing/rule/print",
        "/ip/vrf/print",
        "/ip/route/print",
        "/ipv6/route/print",
        "/routing/filter/rule/print",
        "/routing/filter/select-rule/print",
    ];

    [Fact]
    public void FixedPathsMatchSpecMandatorySectionsExactly()
    {
        Assert.Equal(
            ExpectedFixedPaths.OrderBy(p => p, StringComparer.Ordinal),
            RoutingAssuranceAllowlist.FixedPaths.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(8, RoutingAssuranceAllowlist.FixedPaths.Count);
        Assert.Equal(
            RoutingAssuranceAllowlist.FixedPaths.Count,
            RoutingAssuranceAllowlist.FixedPaths.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CommandIdsLinkToRegistryAndCoverAllFixedPaths()
    {
        Assert.Equal(10, RoutingAssuranceAllowlist.CommandIds.Count);

        HashSet<string> pathsFromCommands = new(StringComparer.Ordinal);
        foreach (RosReadCommandId id in RoutingAssuranceAllowlist.CommandIds)
        {
            RosReadCommandDefinition def = RosReadCommandRegistry.Get(id);
            Assert.EndsWith("/print", def.FixedPath, StringComparison.Ordinal);
            pathsFromCommands.Add(def.FixedPath);
        }

        Assert.Equal(
            RoutingAssuranceAllowlist.FixedPaths.OrderBy(p => p, StringComparer.Ordinal),
            pathsFromCommands.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void IsAllowlistedPathTrueForAllFixedPaths()
    {
        foreach (string path in RoutingAssuranceAllowlist.FixedPaths)
        {
            Assert.True(RosReadCommandRegistry.IsAllowlistedPath(path), path);
        }
    }

    [Fact]
    public void NewCommandsAreRegisteredWithExpectedPaths()
    {
        Assert.Equal(
            "/routing/settings/print",
            RosReadCommandRegistry.Get(RosReadCommandId.RoutingSettings).FixedPath);
        Assert.Equal(
            "/routing/filter/rule/print",
            RosReadCommandRegistry.Get(RosReadCommandId.RoutingFilterRules).FixedPath);
        Assert.Equal(
            "/routing/filter/select-rule/print",
            RosReadCommandRegistry.Get(RosReadCommandId.RoutingFilterSelectRules).FixedPath);

        Assert.Equal(RosResultShape.Singleton, RosReadCommandRegistry.Get(RosReadCommandId.RoutingSettings).ResultShape);
        Assert.Equal(RosRequirement.Required, RosReadCommandRegistry.Get(RosReadCommandId.RoutingSettings).Requirement);
        Assert.Equal(RosRequirement.Conditional, RosReadCommandRegistry.Get(RosReadCommandId.RoutingFilterRules).Requirement);
        Assert.Equal(RosRequirement.Conditional, RosReadCommandRegistry.Get(RosReadCommandId.RoutingFilterSelectRules).Requirement);
    }

    [Fact]
    public void ProfilesExcludeSecretsAndKeepPolicyRulesAndFilterRuleOpaque()
    {
        foreach (RosReadCommandId id in RoutingAssuranceAllowlist.CommandIds)
        {
            string[] props = RosReadCommandRegistry.Get(id).PropertyProfile.ProplistValue.Split(',');
            foreach (string forbidden in RoutingAssuranceAllowlist.ForbiddenPropertyNames)
            {
                Assert.DoesNotContain(forbidden, props, StringComparer.OrdinalIgnoreCase);
            }
        }

        string[] settings = RosReadCommandRegistry.Get(RosReadCommandId.RoutingSettings)
            .PropertyProfile.ProplistValue.Split(',');
        Assert.Contains("policy-rules", settings, StringComparer.Ordinal);

        string[] filterRules = RosReadCommandRegistry.Get(RosReadCommandId.RoutingFilterRules)
            .PropertyProfile.ProplistValue.Split(',');
        Assert.Contains("rule", filterRules, StringComparer.Ordinal);
        Assert.Contains("chain", filterRules, StringComparer.Ordinal);

        Assert.Contains(
            RosReadCommandRegistry.Get(RosReadCommandId.RoutingFilterRules).PropertyProfile.Properties,
            p => p.RouterOsName == "rule" && p.Classification == RosPropertyClassification.ConfigOpaque);
    }

    [Fact]
    public void SensitiveRegistryStillBlocksCredentialAttributes()
    {
        Assert.True(SensitiveFieldRegistry.IsForbidden("password"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("secret"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("private-key"));
    }

    [Fact]
    public void DoesNotOpenRoutingWritePaths()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/settings/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/remove"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/vrf/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ipv6/route/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/filter/rule/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/filter/select-rule/remove"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/filter/rule/set"));
    }

    [Fact]
    public void SharedRoutePrintPathsKeepDistinctQueryProfiles()
    {
        Assert.Equal(
            "/ip/route/print",
            RosReadCommandRegistry.Get(RosReadCommandId.Ipv4StaticRoutes).FixedPath);
        Assert.Equal(
            "/ip/route/print",
            RosReadCommandRegistry.Get(RosReadCommandId.Ipv4DefaultRouteState).FixedPath);
        Assert.NotEqual(
            RosReadCommandRegistry.Get(RosReadCommandId.Ipv4StaticRoutes).QueryProfile.Id,
            RosReadCommandRegistry.Get(RosReadCommandId.Ipv4DefaultRouteState).QueryProfile.Id);

        Assert.Equal(
            "/ipv6/route/print",
            RosReadCommandRegistry.Get(RosReadCommandId.Ipv6StaticRoutes).FixedPath);
        Assert.Equal(
            "/ipv6/route/print",
            RosReadCommandRegistry.Get(RosReadCommandId.Ipv6DefaultRouteState).FixedPath);
    }
}
