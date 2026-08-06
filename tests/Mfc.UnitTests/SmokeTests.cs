using Xunit;

namespace Mfc.UnitTests;

public sealed class SmokeTests
{
    [Fact]
    public void DomainAssemblyMarkerExists()
    {
        Assert.NotNull(typeof(Mfc.Domain.AssemblyMarker));
    }
}
