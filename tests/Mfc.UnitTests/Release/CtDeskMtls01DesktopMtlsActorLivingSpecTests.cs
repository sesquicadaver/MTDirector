using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-MTLS-01 / W7-101: Desktop mTLS actor Living Spec is present and documented.</summary>
public sealed class CtDeskMtls01DesktopMtlsActorLivingSpecTests
{
    [Fact]
    public void Ac1DesktopMtlsActorLivingSpecAndPlan09MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMtlsActorLivingSpecTests.cs");
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopMtlsActorLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3ClientCertificateCnPreferredOverConfiguredActor", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ConfiguredActorIsUsedWhenClientCertificateAbsent", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ConnectedStatusIncludesActorNonConnectedOmitsActor", body, StringComparison.Ordinal);
        Assert.Contains("Ac5ShellAndMainWindowWireStatusTextThroughFormatter", body, StringComparison.Ordinal);
        Assert.Contains("W7-101 DONE", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-MTLS-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopMtlsActorLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-101 Living Spec lock)", limitations, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
