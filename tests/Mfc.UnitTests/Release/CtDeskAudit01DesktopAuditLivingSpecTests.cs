using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-AUDIT-01 / W7-67: Desktop Audit Living Spec is present and documented.</summary>
public sealed class CtDeskAudit01DesktopAuditLivingSpecTests
{
    [Fact]
    public void Ac1DesktopAuditLivingSpecAndPlan05MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopAuditLivingSpecTests.cs");
        string plan05 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-05-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopAuditLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3RefreshLoadsNewestFirstEventsFromClient", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ViewModelIsReadOnlyWithNoWriteCommands", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-67 DONE", plan05, StringComparison.Ordinal);
        Assert.Contains("DESK-AUDIT-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopAuditLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-67 Living Spec lock)", limitations, StringComparison.Ordinal);
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
