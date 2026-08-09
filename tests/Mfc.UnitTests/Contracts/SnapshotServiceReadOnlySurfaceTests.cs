using Google.Protobuf.Reflection;
using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

/// <summary>M1-31 AC#12 / M1-32 AC#12: SnapshotService exposes no WAN/routing/VRRP mutation RPCs.</summary>
public sealed class SnapshotServiceReadOnlySurfaceTests
{
    [Fact]
    public void SnapshotServiceHasNoRoutingOrWanMutationRpcs()
    {
        string[] methods = SnapshotService.Descriptor.Methods
            .Select(m => m.Name)
            .ToArray();
        Assert.DoesNotContain(methods, m => m.Contains("Route", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Wan", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Switch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Write", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Set", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("StartCapture", methods);
        Assert.Contains("CompareSnapshots", methods);
    }

    [Fact]
    public void SnapshotServiceHasNoVrrpMutationRpcs()
    {
        string[] methods = SnapshotService.Descriptor.Methods
            .Select(m => m.Name)
            .ToArray();
        Assert.DoesNotContain(methods, m => m.Contains("Vrrp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Priority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Failover", StringComparison.OrdinalIgnoreCase));
    }
}
