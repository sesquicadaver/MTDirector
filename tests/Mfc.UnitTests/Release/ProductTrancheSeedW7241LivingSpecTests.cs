using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-241: known-limitations / queue seed locked DESK-A11Y-PANEL-01 (W7-242) after SNAP-01;
/// historical: PANEL-01 DONE; W7-243 PLAN-27 COMPLETE seed DONE; NEXT advanced to W7-244.
/// </summary>
public sealed class ProductTrancheSeedW7241LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yPanel01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));

        Assert.Contains("Intentional residual (W7-241 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-242", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-243", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-242**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-241 | [#887](https://github.com/sesquicadaver/MTDirector/issues/887) | Seed next PLAN-27 row after DESK-A11Y-SNAP-01 → DESK-A11Y-PANEL-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-242 | [#891](https://github.com/sesquicadaver/MTDirector/issues/891) | DESK-A11Y-PANEL-01 — Node Refresh / Validate / Drift / Audit Refresh AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-243 | [#892](https://github.com/sesquicadaver/MTDirector/issues/892) | Seed next after DESK-A11Y-PANEL-01 (PLAN-27 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-322 (#1050)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-241 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-242", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-243 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-322 (#1050)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-241 (#887) DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-242 (#891) DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-243 (#892) DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-322 (#1050)", plan27, StringComparison.Ordinal);
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
