using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-277: PLAN-33 COMPLETE; known-limitations / queue seed locked PLAN-34 inventory (W7-278)
/// and follow-up seed W7-279 after DESK-A11Y-TREE-01.
/// </summary>
public sealed class ProductTrancheSeedW7277LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan34AfterPlan33Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan33 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-33-desktop-inventory-treeview-a11y.md"));
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));

        Assert.Contains("Intentional residual (W7-277 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-33 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-34", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-278", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-279", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-278**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-277 | [#959](https://github.com/sesquicadaver/MTDirector/issues/959) | Seed next after DESK-A11Y-TREE-01 (PLAN-33 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-278 | [#963](https://github.com/sesquicadaver/MTDirector/issues/963) | PLAN-34 — Inventory Desktop operator launch packaging templates (.desktop / Windows shortcut) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-279 | [#964](https://github.com/sesquicadaver/MTDirector/issues/964) | Seed first PLAN-34 atomic row after inventory → DESK-HOST-LINUX-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-340 (#1087)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-33 COMPLETE", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-277 (#959) DONE", plan33, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-340 (#1087)", plan33, StringComparison.Ordinal);
        Assert.Contains("plan-34-desktop-operator-launch-packaging.md", plan33, StringComparison.Ordinal);

        Assert.Contains("PLAN-34", plan, StringComparison.Ordinal);
        Assert.Contains("W7-278", plan, StringComparison.Ordinal);
        Assert.Contains("W7-277 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-340 (#1087)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-34-desktop-operator-launch-packaging.md", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-HOST-LINUX-01", plan34, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-278", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-279", plan34, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-340 (#1087)", plan34, StringComparison.Ordinal);
        Assert.Contains("3e112bf", plan34, StringComparison.Ordinal);
        Assert.Contains("package-desktop.sh", plan34, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/desktop", plan34, StringComparison.Ordinal);

        Assert.Contains("OUT_DIR/desktop", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("zip/tar", packaging, StringComparison.OrdinalIgnoreCase);
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
