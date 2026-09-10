using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-184: PLAN-21 inventory documents ranked DESK-A11Y-POLICY-EDIT-* rows and seeds DESK-A11Y-POLICY-EDIT-01.</summary>
public sealed class Plan21DesktopPoliciesAuthoringResidualW7184LivingSpecTests
{
    [Fact]
    public void Ac1Plan21InventoryDocumentsRankedRowsAndSeedsDeskA11yPolicyEdit01()
    {
        string root = RepoRoot();
        string plan21 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-21-desktop-policies-authoring-residual-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-21 — Desktop Policies authoring residual AutomationProperties Living Spec product tranche", plan21, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-01", plan21, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-02", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-185", plan21, StringComparison.Ordinal);
        Assert.Contains("Create draft", plan21, StringComparison.Ordinal);
        Assert.Contains("Load", plan21, StringComparison.Ordinal);
        Assert.Contains("Compose", plan21, StringComparison.Ordinal);
        Assert.Contains("Diff", plan21, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan21, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan21, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-184 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-21", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-185", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-21", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-21-desktop-policies-authoring-residual-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-21-desktop-policies-authoring-residual-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan20, StringComparison.Ordinal);
        Assert.Contains("PLAN-21", plan20, StringComparison.Ordinal);
        Assert.Contains("Plan21DesktopPoliciesAuthoringResidualW7184", testing, StringComparison.Ordinal);
        Assert.Contains("Policies.CreateDraftCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.ComposeCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffCommand", main, StringComparison.Ordinal);
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
