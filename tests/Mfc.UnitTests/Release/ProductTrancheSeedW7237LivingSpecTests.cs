using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-237: PLAN-26 COMPLETE; known-limitations / queue seed locked PLAN-27 inventory (W7-238)
/// and follow-up seed W7-239 after AUDIT-INT-01. Historical: inventory DONE; SNAP/PANEL DONE; PLAN-27 COMPLETE; NEXT advanced to W7-244.
/// </summary>
public sealed class ProductTrancheSeedW7237LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan27AfterPlan26Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));

        Assert.Contains("Intentional residual (W7-237 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-26 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-27", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-238", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-239", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-237 | [#880](https://github.com/sesquicadaver/MTDirector/issues/880) | Seed next after AUDIT-INT-01 (PLAN-26 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-238 | [#883](https://github.com/sesquicadaver/MTDirector/issues/883) | PLAN-27 — Inventory Desktop Snapshot/Node/Drift/Audit AutomationProperties residual tranche after PLAN-26 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-239 | [#884](https://github.com/sesquicadaver/MTDirector/issues/884) | Seed first PLAN-27 atomic row after inventory → DESK-A11Y-SNAP-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-26 COMPLETE", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-237 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", plan26, StringComparison.Ordinal);

        Assert.Contains("PLAN-27", plan, StringComparison.Ordinal);
        Assert.Contains("W7-238", plan, StringComparison.Ordinal);
        Assert.Contains("W7-237 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-SNAP-01", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan27, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", plan27, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-238**", limitations, StringComparison.Ordinal);
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
