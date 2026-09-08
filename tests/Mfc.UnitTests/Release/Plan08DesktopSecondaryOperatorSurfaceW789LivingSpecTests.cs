using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-89: PLAN-08 Desktop secondary operator-surface inventory documents ranked DESK-* rows and seeds DESK-NODE-01.</summary>
public sealed class Plan08DesktopSecondaryOperatorSurfaceW789LivingSpecTests
{
    [Fact]
    public void Ac1Plan08InventoryDocumentsRankedSecondaryRowsAndSeedsDeskNode01()
    {
        string root = RepoRoot();
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));

        Assert.Contains("PLAN-08 — Desktop secondary operator-surface Living Spec product tranche", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-NODE-01", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-NBR-01", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-PROBE-01", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-02", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-90", plan08, StringComparison.Ordinal);
        Assert.Contains("NodeDetailViewModel", plan08, StringComparison.Ordinal);
        Assert.Contains("ValidateVrrpPairConsistency", plan08, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-89 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-NODE-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-90", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-08-desktop-secondary-operator-surface.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-08-desktop-secondary-operator-surface.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan07, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", plan07, StringComparison.Ordinal);
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
