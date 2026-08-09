using Google.Protobuf.Reflection;
using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

/// <summary>M1-31 AC#12: SnapshotService exposes no WAN/routing mutation RPCs.</summary>
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
}
