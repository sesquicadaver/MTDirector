using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-247: known-limitations / queue seed locks DESK-A11Y-CTRL-01 (W7-248) as §3.C NEXT after FIELD-01;
/// follow-up W7-249 PLAN-28 COMPLETE seed opened.
/// </summary>
public sealed class ProductTrancheSeedW7247LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yCtrl01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));

        Assert.Contains("Intentional residual (W7-247 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-248", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-249", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-248**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-247 | [#899](https://github.com/sesquicadaver/MTDirector/issues/899) | Seed next PLAN-28 row after DESK-A11Y-FIELD-01 → DESK-A11Y-CTRL-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-248 | [#903](https://github.com/sesquicadaver/MTDirector/issues/903) | DESK-A11Y-CTRL-01 — Snapshot/Diff ComboBox & CheckBox + TabItem AutomationProperties.Name | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-249 | [#904](https://github.com/sesquicadaver/MTDirector/issues/904) | Seed next after DESK-A11Y-CTRL-01 (PLAN-28 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-248 (#903)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-247 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-248", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-249", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-248 (#903)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-247 (#899) DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-248 (#903)", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-249 (#904)", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-248 (#903)", plan28, StringComparison.Ordinal);
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
