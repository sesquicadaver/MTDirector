using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-261: PLAN-30 COMPLETE; known-limitations / queue seed locked PLAN-31 inventory (W7-262)
/// and follow-up seed W7-263 after WATCH-BP-01.
/// Historical: inventory DONE; LIST/RO opened; seed W7-263 DONE → DESK-A11Y-LIST-01; NEXT advanced to W7-264.
/// </summary>
public sealed class ProductTrancheSeedW7261LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan31AfterPlan30Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));
        string mainWindow = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("Intentional residual (W7-261 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-30 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-31", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-262", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-263", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-LIST-01", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-RO-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-262**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-261 | [#928](https://github.com/sesquicadaver/MTDirector/issues/928) | Seed next after WATCH-BP-01 (PLAN-30 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-262 | [#931](https://github.com/sesquicadaver/MTDirector/issues/931) | PLAN-31 — Inventory Desktop residual ListBox / Drift–Audit read-only a11y (PLAN-28 deferred) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-263 | [#932](https://github.com/sesquicadaver/MTDirector/issues/932) | Seed first PLAN-31 atomic row after inventory → DESK-A11Y-LIST-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-279 (#964)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-30 COMPLETE", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-261 DONE", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-279 (#964)", plan30, StringComparison.Ordinal);

        Assert.Contains("PLAN-31", plan, StringComparison.Ordinal);
        Assert.Contains("W7-262", plan, StringComparison.Ordinal);
        Assert.Contains("W7-261 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-279 (#964)", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-LIST-01", plan31, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-RO-01", plan31, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan31, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-279 (#964)", plan31, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", plan31, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent.PayloadJson", plan31, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Drift.Events}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent.PayloadJson", mainWindow, StringComparison.Ordinal);
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
