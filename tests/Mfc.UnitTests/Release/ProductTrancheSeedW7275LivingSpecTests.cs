using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-275: known-limitations / queue seed locked DESK-A11Y-TREE-01 (W7-276) after PLAN-33 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7275LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yTree01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan33 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-33-desktop-inventory-treeview-a11y.md"));
        string axaml = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("Intentional residual (W7-275 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TREE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-276", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-277", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-276**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-275 | [#956](https://github.com/sesquicadaver/MTDirector/issues/956) | Seed first PLAN-33 atomic row after inventory → DESK-A11Y-TREE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-276 | [#958](https://github.com/sesquicadaver/MTDirector/issues/958) | DESK-A11Y-TREE-01 — Inventory TreeView AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-277 | [#959](https://github.com/sesquicadaver/MTDirector/issues/959) | Seed next after DESK-A11Y-TREE-01 (PLAN-33 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-409 (#1218)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-275", plan, StringComparison.Ordinal);
        Assert.Contains("W7-276", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TREE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-409 (#1218)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-275 (#956) DONE", plan33, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TREE-01", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-276", plan33, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-409 (#1218)", plan33, StringComparison.Ordinal);

        // Historical seed: TREE-01 later implemented (Name present)
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
