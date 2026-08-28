using Google.Protobuf.Reflection;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Inventory;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.RouterOs.Commands;
using Xunit;

namespace Mfc.UnitTests.Inventory;

/// <summary>
/// Living Spec: seed MikroTik neighbor candidates (#314).
/// ТЗ → module → tests: on-demand Controller-mediated /ip/neighbor suggestions only.
/// </summary>
public sealed class NeighborCandidatesLivingSpecTests
{
    [Fact]
    public void Ac1AllowlistIsPrintOnlyIpNeighborDistinctFromIpv6Nd()
    {
        Assert.All(NeighborDiscoveryAllowlist.FixedPaths, p => Assert.EndsWith("/print", p, StringComparison.Ordinal));
        Assert.Contains("/ip/neighbor/print", NeighborDiscoveryAllowlist.FixedPaths);
        Assert.DoesNotContain(NeighborDiscoveryAllowlist.FixedPaths, p => p.Contains("/add", StringComparison.Ordinal));
        Assert.Equal("/ip/neighbor/print", RosReadCommandRegistry.Get(RosReadCommandId.IpNeighbors).FixedPath);
        Assert.Equal("/ipv6/neighbor/print", RosReadCommandRegistry.Get(RosReadCommandId.Ipv6Neighbors).FixedPath);
    }

    [Fact]
    public void Ac2FilterKeepsOnlyMikroTikAndSkipsEmptyOrKnownHosts()
    {
        Assert.True(NeighborCandidateFilter.IsMikroTikPlatform("MikroTik"));
        Assert.False(NeighborCandidateFilter.IsMikroTikPlatform("Cisco"));

        IReadOnlyList<NeighborCandidateView> selected = NeighborCandidateFilter.SelectMikroTikCandidates(
            [
                new RouterOsNeighborRow { Address = "10.0.0.2", Platform = "MikroTik", Identity = "a" },
                new RouterOsNeighborRow { Address = "10.0.0.3", Platform = "Other", Identity = "b" },
            ],
            seedIdentity: "seed",
            knownManagementHosts: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "10.0.0.2" });

        Assert.Empty(selected);
    }

    [Fact]
    public void Ac3InventoryRpcExposesListNeighborCandidatesWithoutAutoRegisterSemantics()
    {
        ServiceDescriptor descriptor = InventoryService.Descriptor;
        Assert.Contains(descriptor.Methods, m => m.Name == "ListNeighborCandidates");
        Assert.DoesNotContain(
            NeighborCandidate.Descriptor.Fields.InDeclarationOrder(),
            f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac4DesktopClientSurfacesNeighborRpcContractsOnly()
    {
        Type inventory = typeof(IInventoryTreeClient);
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ListNeighborCandidatesAsync)));
        Assert.Null(inventory.Assembly.GetType("Mfc.RouterOs.Commands.RosReadCommandId"));
    }
}
