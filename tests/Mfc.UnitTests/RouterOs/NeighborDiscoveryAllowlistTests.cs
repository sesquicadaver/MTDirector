using Mfc.RouterOs.Commands;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class NeighborDiscoveryAllowlistTests
{
    [Fact]
    public void FixedPathsMatchAllowlistExactly()
    {
        Assert.Single(NeighborDiscoveryAllowlist.FixedPaths);
        Assert.Equal(
            NeighborDiscoveryAllowlist.FixedPaths.OrderBy(p => p, StringComparer.Ordinal),
            NeighborDiscoveryAllowlist.CommandIds
                .Select(id => RosReadCommandRegistry.Get(id).FixedPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void ProfilesExcludeSecrets()
    {
        foreach (RosReadCommandId id in NeighborDiscoveryAllowlist.CommandIds)
        {
            string[] props = RosReadCommandRegistry.Get(id).PropertyProfile.ProplistValue.Split(',');
            foreach (string forbidden in NeighborDiscoveryAllowlist.ForbiddenPropertyNames)
            {
                Assert.DoesNotContain(forbidden, props, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void DoesNotOpenWritePaths()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/neighbor/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/neighbor/remove"));
        Assert.True(RosReadCommandRegistry.IsAllowlistedPath("/ip/neighbor/print"));
    }

    [Fact]
    public void IpNeighborsIsDistinctFromIpv6NdAttributionPath()
    {
        Assert.Equal("/ip/neighbor/print", RosReadCommandRegistry.Get(RosReadCommandId.IpNeighbors).FixedPath);
        Assert.Equal("/ipv6/neighbor/print", RosReadCommandRegistry.Get(RosReadCommandId.Ipv6Neighbors).FixedPath);
    }
}
