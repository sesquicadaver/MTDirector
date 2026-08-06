using Xunit;

namespace Mfc.RouterOs.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void RouterOsAssemblyMarkerExists()
    {
        Assert.NotNull(typeof(Mfc.RouterOs.AssemblyMarker));
    }
}
