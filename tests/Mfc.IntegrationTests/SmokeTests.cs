using Xunit;

namespace Mfc.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void InfrastructureAssemblyMarkerExists()
    {
        Assert.NotNull(typeof(Mfc.Infrastructure.AssemblyMarker));
    }
}
