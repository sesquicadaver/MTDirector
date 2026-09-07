using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-66: PLAN-05 Desktop operator-surface inventory documents ranked DESK rows and seeds DESK-AUDIT-01.</summary>
public sealed class Plan05DesktopOperatorSurfaceW766LivingSpecTests
{
    [Fact]
    public void Ac1Plan05InventoryDocumentsRankedDeskRowsAndSeedsAuditDesktop()
    {
        string root = RepoRoot();
        string plan05 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-05-desktop-operator-surface.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));

        Assert.Contains("PLAN-05 — Desktop operator-surface Living Spec product tranche", plan05, StringComparison.Ordinal);
        Assert.Contains("DESK-AUDIT-01", plan05, StringComparison.Ordinal);
        Assert.Contains("DESK-DRIFT-01", plan05, StringComparison.Ordinal);
        Assert.Contains("DESK-ZONE-01", plan05, StringComparison.Ordinal);
        Assert.Contains("DESK-ROUTING-01", plan05, StringComparison.Ordinal);
        Assert.Contains("W7-67", plan05, StringComparison.Ordinal);
        Assert.Contains("AuditViewModel", plan05, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-66 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-05", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-AUDIT-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-67", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-05", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-05-desktop-operator-surface.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-05-desktop-operator-surface.md", docsIndex, StringComparison.Ordinal);
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
