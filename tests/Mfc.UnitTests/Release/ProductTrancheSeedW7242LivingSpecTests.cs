using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-242: DESK-A11Y-PANEL-01 DONE; historical NEXT was W7-243 PLAN-27 COMPLETE seed;
/// W7-243 advanced §3.C NEXT to W7-244 PLAN-28 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7242LivingSpecTests
{
    [Fact]
    public void Ac1Panel01DoneAndQueueAdvancesToPlan27CompleteSeed()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));

        Assert.Contains("Intentional residual (W7-242 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("DesktopPanelAutomationLivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-243", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-27 COMPLETE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-242 | [#891](https://github.com/sesquicadaver/MTDirector/issues/891) | DESK-A11Y-PANEL-01 — Node Refresh / Validate / Drift / Audit Refresh AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-243 | [#892](https://github.com/sesquicadaver/MTDirector/issues/892) | Seed next after DESK-A11Y-PANEL-01 (PLAN-27 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-242 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-243 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-242 DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-243 (#892) DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", plan27, StringComparison.Ordinal);
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
