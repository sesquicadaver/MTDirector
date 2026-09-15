using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-246: DESK-A11Y-FIELD-01 DONE; historical NEXT was W7-247 CTRL-01 seed;
/// W7-247/W7-248 advanced §3.C NEXT to W7-249 PLAN-28 COMPLETE seed.
/// </summary>
public sealed class ProductTrancheSeedW7246LivingSpecTests
{
    [Fact]
    public void Ac1Field01DoneAndQueueAdvancesToDeskA11yCtrl01Seed()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));

        Assert.Contains("Intentional residual (W7-246 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("DesktopZonesPoliciesFieldAutomationLivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-247", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-246 | [#898](https://github.com/sesquicadaver/MTDirector/issues/898) | DESK-A11Y-FIELD-01 — Zones / Policies draft TextBox AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-247 | [#899](https://github.com/sesquicadaver/MTDirector/issues/899) | Seed next PLAN-28 row after DESK-A11Y-FIELD-01 → DESK-A11Y-CTRL-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-256 (#919)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-246 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-247 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-256 (#919)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-246 DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-247 (#899) DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-256 (#919)", plan28, StringComparison.Ordinal);
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
