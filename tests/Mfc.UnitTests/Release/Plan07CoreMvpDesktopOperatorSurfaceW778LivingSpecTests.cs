using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-78: PLAN-07 Core MVP Desktop operator-surface inventory documents ranked DESK-* rows and seeds DESK-POLICY-01.</summary>
public sealed class Plan07CoreMvpDesktopOperatorSurfaceW778LivingSpecTests
{
    [Fact]
    public void Ac1Plan07InventoryDocumentsRankedCoreMvpRowsAndSeedsDeskPolicy01()
    {
        string root = RepoRoot();
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));

        Assert.Contains("PLAN-07 — Core MVP Desktop operator-surface Living Spec product tranche", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-01", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-DEPLOY-01", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-ONBOARD-01", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-SNAPSHOT-01", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-INVENTORY-01", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-79", plan07, StringComparison.Ordinal);
        Assert.Contains("PoliciesViewModel", plan07, StringComparison.Ordinal);
        Assert.Contains("PolicyGrpcHostTests", plan07, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-78 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-07", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-79", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-07", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-07-core-mvp-desktop-operator-surface.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-07-core-mvp-desktop-operator-surface.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan06, StringComparison.Ordinal);
        Assert.Contains("PLAN-07", plan06, StringComparison.Ordinal);
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
