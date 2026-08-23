using Mfc.RouterOs.Commands;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class ConnectionTrackingAllowlistTests
{
    [Fact]
    public void FixedPathsMatchAllowlistExactly()
    {
        Assert.Equal(2, ConnectionTrackingAllowlist.FixedPaths.Count);
        Assert.Equal(
            ConnectionTrackingAllowlist.FixedPaths.OrderBy(p => p, StringComparer.Ordinal),
            ConnectionTrackingAllowlist.CommandIds
                .Select(id => RosReadCommandRegistry.Get(id).FixedPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void ProfilesExcludeSecrets()
    {
        foreach (RosReadCommandId id in ConnectionTrackingAllowlist.CommandIds)
        {
            string[] props = RosReadCommandRegistry.Get(id).PropertyProfile.ProplistValue.Split(',');
            foreach (string forbidden in ConnectionTrackingAllowlist.ForbiddenPropertyNames)
            {
                Assert.DoesNotContain(forbidden, props, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void DoesNotOpenWritePaths()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/firewall/connection/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ipv6/firewall/connection/remove"));
    }
}
