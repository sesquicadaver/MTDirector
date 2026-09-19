using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-243: PLAN-27 COMPLETE; known-limitations / queue seed locked PLAN-28 inventory (W7-244)
/// and follow-up seed W7-245 after DESK-A11Y-PANEL-01.
/// Historical: inventory DONE; FIELD/CTRL DONE; PLAN-28 COMPLETE; NEXT advanced to W7-251.
/// </summary>
public sealed class ProductTrancheSeedW7243LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan28AfterPlan27Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));

        Assert.Contains("Intentional residual (W7-243 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-27 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-28", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-244", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-245", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-243 | [#892](https://github.com/sesquicadaver/MTDirector/issues/892) | Seed next after DESK-A11Y-PANEL-01 (PLAN-27 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-244 | [#895](https://github.com/sesquicadaver/MTDirector/issues/895) | PLAN-28 — Inventory Desktop residual field/control AutomationProperties tranche after PLAN-27 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-245 | [#896](https://github.com/sesquicadaver/MTDirector/issues/896) | Seed first PLAN-28 atomic row after inventory → DESK-A11Y-FIELD-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-355 (#1115)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-27 COMPLETE", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-243 DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-355 (#1115)", plan27, StringComparison.Ordinal);

        Assert.Contains("PLAN-28", plan, StringComparison.Ordinal);
        Assert.Contains("W7-244", plan, StringComparison.Ordinal);
        Assert.Contains("W7-243 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-355 (#1115)", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-FIELD-01", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", plan28, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan28, StringComparison.Ordinal);
        Assert.Contains("PLAN-28 COMPLETE", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-355 (#1115)", plan28, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-244**", limitations, StringComparison.Ordinal);
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
