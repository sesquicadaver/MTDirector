using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-98: PLAN-09 Desktop connection-status operator-surface inventory documents ranked DESK-* rows and seeds DESK-CONN-01.</summary>
public sealed class Plan09DesktopConnectionStatusOperatorSurfaceW798LivingSpecTests
{
    [Fact]
    public void Ac1Plan09InventoryDocumentsRankedConnectionRowsAndSeedsDeskConn01()
    {
        string root = RepoRoot();
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-09 — Desktop connection-status operator-surface Living Spec product tranche", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-01", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-MTLS-01", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-AUTH-01", plan09, StringComparison.Ordinal);
        Assert.Contains("W7-99", plan09, StringComparison.Ordinal);
        Assert.Contains("IControllerConnectionService", plan09, StringComparison.Ordinal);
        Assert.Contains("DesktopConnectionStatusText", plan09, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-98 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-09", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-99", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-09", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-09-desktop-connection-status-operator-surface.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-09-desktop-connection-status-operator-surface.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan08, StringComparison.Ordinal);
        Assert.Contains("PLAN-09", plan08, StringComparison.Ordinal);
        Assert.Contains("Plan09DesktopConnectionStatusOperatorSurfaceW798", testing, StringComparison.Ordinal);
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
