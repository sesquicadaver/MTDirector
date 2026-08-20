using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class AuditProtoContractTests
{
    [Fact]
    public void AuditServiceExposesOnlyListRpc()
    {
        string[] methods = AuditService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["ListAuditEvents"], methods);
        Assert.Equal("mfc.v1.AuditService", AuditService.Descriptor.FullName);
    }

    [Fact]
    public void AuditContractHasNoMutateSurface()
    {
        foreach (string name in AuditService.Descriptor.Methods.Select(static m => m.Name)
                     .Concat(AuditService.Descriptor.File.MessageTypes.Select(static m => m.Name)))
        {
            Assert.DoesNotContain("Append", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Write", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Delete", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Update", name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
