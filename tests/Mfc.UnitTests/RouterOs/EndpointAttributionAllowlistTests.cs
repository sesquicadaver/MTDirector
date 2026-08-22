using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Redaction;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class EndpointAttributionAllowlistTests
{
    [Fact]
    public void FixedPathsMatchAllowlistExactly()
    {
        Assert.Equal(8, EndpointAttributionAllowlist.FixedPaths.Count);
        Assert.Equal(
            EndpointAttributionAllowlist.FixedPaths.OrderBy(p => p, StringComparer.Ordinal),
            EndpointAttributionAllowlist.CommandIds
                .Select(id => RosReadCommandRegistry.Get(id).FixedPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void CommandIdsLinkToRegistry()
    {
        foreach (RosReadCommandId id in EndpointAttributionAllowlist.CommandIds)
        {
            RosReadCommandDefinition def = RosReadCommandRegistry.Get(id);
            Assert.EndsWith("/print", def.FixedPath, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProfilesExcludeSecrets()
    {
        foreach (RosReadCommandId id in EndpointAttributionAllowlist.CommandIds)
        {
            string[] props = RosReadCommandRegistry.Get(id).PropertyProfile.ProplistValue.Split(',');
            foreach (string forbidden in EndpointAttributionAllowlist.ForbiddenPropertyNames)
            {
                Assert.DoesNotContain(forbidden, props, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void DoesNotOpenWritePaths()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/arp/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ipv6/neighbor/remove"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/interface/wireguard/peers/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ppp/active/remove"));
    }

    [Fact]
    public void SensitiveRegistryStillBlocksCredentialAttributes()
    {
        Assert.True(SensitiveFieldRegistry.IsForbidden("private-key"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("psk"));
    }
}
