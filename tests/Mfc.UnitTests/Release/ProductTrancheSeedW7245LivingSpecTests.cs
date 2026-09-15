using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-245: known-limitations / queue seed locked DESK-A11Y-FIELD-01 (W7-246) after PLAN-28 inventory.
/// Historical: FIELD-01 DONE; NEXT advanced to W7-247 CTRL-01 seed.
/// </summary>
public sealed class ProductTrancheSeedW7245LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yField01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));

        Assert.Contains("Intentional residual (W7-245 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-246", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-247", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-245 | [#896](https://github.com/sesquicadaver/MTDirector/issues/896) | Seed first PLAN-28 atomic row after inventory → DESK-A11Y-FIELD-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-246 | [#898](https://github.com/sesquicadaver/MTDirector/issues/898) | DESK-A11Y-FIELD-01 — Zones / Policies draft TextBox AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-257 (#920)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-245 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-246", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-257 (#920)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-245 DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-246", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-257 (#920)", plan28, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-246**", limitations, StringComparison.Ordinal);
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
