using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-273: PLAN-32 COMPLETE; known-limitations / queue seed locked PLAN-33 inventory (W7-274)
/// and follow-up seed W7-275 after OPS-HOST-WINSVC-01. Historical: inventory later DONE; NEXT advanced.
/// </summary>
public sealed class ProductTrancheSeedW7273LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan33AfterPlan32Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));
        string plan33 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-33-desktop-inventory-treeview-a11y.md"));
        string axaml = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("Intentional residual (W7-273 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-32 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-33", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-274", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-275", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TREE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-274**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-273 | [#952](https://github.com/sesquicadaver/MTDirector/issues/952) | Seed next after OPS-HOST-WINSVC-01 (PLAN-32 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-274 | [#955](https://github.com/sesquicadaver/MTDirector/issues/955) | PLAN-33 — Inventory Desktop Inventory TreeView / residual TabControl a11y | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-275 | [#956](https://github.com/sesquicadaver/MTDirector/issues/956) | Seed first PLAN-33 atomic row after inventory → DESK-A11Y-TREE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-32 COMPLETE", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-273 (#952) DONE", plan32, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", plan32, StringComparison.Ordinal);
        Assert.Contains("plan-33-desktop-inventory-treeview-a11y.md", plan32, StringComparison.Ordinal);

        Assert.Contains("PLAN-33", plan, StringComparison.Ordinal);
        Assert.Contains("W7-274", plan, StringComparison.Ordinal);
        Assert.Contains("W7-273 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-33-desktop-inventory-treeview-a11y.md", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-TREE-01", plan33, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-274", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-275", plan33, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", plan33, StringComparison.Ordinal);
        Assert.Contains("50f1ae1", plan33, StringComparison.Ordinal);
        Assert.Contains("Inventory.Roots", plan33, StringComparison.Ordinal);

        // Evidence lock: Inventory TreeView exposes AutomationProperties.Name after TREE-01
        const string rootsBinding = "ItemsSource=\"{Binding Inventory.Roots}\"";
        Assert.Contains(rootsBinding, axaml, StringComparison.Ordinal);
        int treeIdx = axaml.IndexOf(rootsBinding, StringComparison.Ordinal);
        Assert.True(treeIdx > 0);
        int regionStart = Math.Max(0, treeIdx - 80);
        int regionLen = Math.Min(350, axaml.Length - regionStart);
        string openRegion = axaml.Substring(regionStart, regionLen);
        Assert.Contains("<TreeView", openRegion, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Inventory\"", openRegion, StringComparison.Ordinal);
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
