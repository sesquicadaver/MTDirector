using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-AUTH-01 / W7-103: Desktop Auth fail-closed Living Spec is present and documented.</summary>
public sealed class CtDeskAuth01DesktopAuthLivingSpecTests
{
    [Fact]
    public void Ac1DesktopAuthLivingSpecAndPlan09MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopAuthLivingSpecTests.cs");
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopAuthLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2FormatterLocksAuthenticationFailedAndTlsErrorLabelsWithoutActor", body, StringComparison.Ordinal);
        Assert.Contains("Ac3ControllerConnectionServiceMapsUnauthenticatedAndTlsFailures", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ShellCanReconnectFromAuthFailedOrTlsErrorAndSurfacesLastError", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsStatusAndErrorForFailClosedAuthPath", body, StringComparison.Ordinal);
        Assert.Contains("W7-103 DONE", plan09, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-AUTH-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopAuthLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-103 Living Spec lock)", limitations, StringComparison.Ordinal);
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
