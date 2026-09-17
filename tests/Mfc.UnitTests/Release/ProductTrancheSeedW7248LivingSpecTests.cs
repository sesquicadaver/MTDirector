using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-248: DESK-A11Y-CTRL-01 DONE; historical NEXT advanced to W7-249 then W7-250 after PLAN-28 COMPLETE seed.
/// </summary>
public sealed class ProductTrancheSeedW7248LivingSpecTests
{
    [Fact]
    public void Ac1Ctrl01DoneAndQueueAdvancesToPlan28CompleteSeed()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));

        Assert.Contains("Intentional residual (W7-248 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("DesktopSnapshotDiffControlAutomationLivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-249", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-28 COMPLETE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-248 | [#903](https://github.com/sesquicadaver/MTDirector/issues/903) | DESK-A11Y-CTRL-01 — Snapshot/Diff ComboBox & CheckBox + TabItem AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-249 | [#904](https://github.com/sesquicadaver/MTDirector/issues/904) | Seed next after DESK-A11Y-CTRL-01 (PLAN-28 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-315 (#1036)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-248 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-249", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-315 (#1036)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-248 DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-249 (#904)", plan28, StringComparison.Ordinal);
        Assert.Contains("PLAN-28 COMPLETE", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-315 (#1036)", plan28, StringComparison.Ordinal);
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
