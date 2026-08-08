using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Redaction;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class PacketPathAllowlistTests
{
    [Fact]
    public void RegistersContainerAppVethAndVrfPrintCommands()
    {
        Assert.Equal(4, PacketPathAllowlist.CommandIds.Count);
        Assert.Equal(
            PacketPathAllowlist.FixedPaths.OrderBy(p => p, StringComparer.Ordinal),
            PacketPathAllowlist.CommandIds
                .Select(id => RosReadCommandRegistry.Get(id).FixedPath)
                .OrderBy(p => p, StringComparer.Ordinal));

        Assert.True(RosReadCommandRegistry.IsAllowlistedPath("/container/print"));
        Assert.True(RosReadCommandRegistry.IsAllowlistedPath("/app/print"));
        Assert.True(RosReadCommandRegistry.IsAllowlistedPath("/interface/veth/print"));
        Assert.True(RosReadCommandRegistry.IsAllowlistedPath("/ip/vrf/print"));
    }

    [Fact]
    public void AppsCommandIsOptionalWhenSubsystemMissing()
    {
        RosReadCommandDefinition apps = RosReadCommandRegistry.Get(RosReadCommandId.Apps);
        Assert.Equal(RosRequirement.Optional, apps.Requirement);
        Assert.Equal("/app/print", apps.FixedPath);
    }

    [Fact]
    public void NetworkSignificantProplistsExcludeSecretsEnvMountsAndShellPayload()
    {
        foreach (RosReadCommandId id in PacketPathAllowlist.CommandIds)
        {
            string[] props = RosReadCommandRegistry.Get(id).PropertyProfile.ProplistValue.Split(',');
            foreach (string forbidden in PacketPathAllowlist.ForbiddenPropertyNames)
            {
                Assert.DoesNotContain(forbidden, props, StringComparer.OrdinalIgnoreCase);
            }

            Assert.EndsWith("/print", RosReadCommandRegistry.Get(id).FixedPath, StringComparison.Ordinal);
        }

        Assert.Contains(
            "interface",
            RosReadCommandRegistry.Get(RosReadCommandId.Containers).PropertyProfile.ProplistValue.Split(','),
            StringComparer.Ordinal);
        Assert.Contains(
            "address",
            RosReadCommandRegistry.Get(RosReadCommandId.VethInterfaces).PropertyProfile.ProplistValue.Split(','),
            StringComparer.Ordinal);
        Assert.Contains(
            "interfaces",
            RosReadCommandRegistry.Get(RosReadCommandId.IpVrfs).PropertyProfile.ProplistValue.Split(','),
            StringComparer.Ordinal);
    }

    [Fact]
    public void SensitiveRegistryBlocksContainerEnvAndMountAttributes()
    {
        Assert.True(SensitiveFieldRegistry.IsForbidden("envlist"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("mountlists"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("env"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("mounts"));
    }

    [Fact]
    public void DoesNotOpenWriteOrShellPaths()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/container/shell"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/container/run"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/container/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/app/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/interface/veth/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/vrf/add"));
    }
}
