using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-179: PLAN-20 inventory documents ranked DESK-A11Y-POLICY-* rows and seeds DESK-A11Y-POLICY-01.</summary>
public sealed class Plan20DesktopPoliciesLifecycleActionW7179LivingSpecTests
{
    [Fact]
    public void Ac1Plan20InventoryDocumentsRankedRowsAndSeedsDeskA11yPolicy01()
    {
        string root = RepoRoot();
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-20 — Desktop Policies lifecycle-action AutomationProperties Living Spec product tranche", plan20, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-01", plan20, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-02", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-180", plan20, StringComparison.Ordinal);
        Assert.Contains("Validate", plan20, StringComparison.Ordinal);
        Assert.Contains("Submit for review", plan20, StringComparison.Ordinal);
        Assert.Contains("Approve", plan20, StringComparison.Ordinal);
        Assert.Contains("Bind", plan20, StringComparison.Ordinal);
        Assert.Contains("Deploy", plan20, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan20, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan20, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-179 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-20", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-180", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-20", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-20-desktop-policies-lifecycle-action-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-20-desktop-policies-lifecycle-action-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan19, StringComparison.Ordinal);
        Assert.Contains("PLAN-20", plan19, StringComparison.Ordinal);
        Assert.Contains("Plan20DesktopPoliciesLifecycleActionW7179", testing, StringComparison.Ordinal);
        Assert.Contains("Policies.ValidateCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.DeployCommand", main, StringComparison.Ordinal);
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
