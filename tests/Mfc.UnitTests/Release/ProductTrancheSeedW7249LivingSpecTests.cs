using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-249: PLAN-28 COMPLETE; known-limitations / queue seed locked PLAN-29 inventory (W7-250)
/// and follow-up seed W7-251 after DESK-A11Y-CTRL-01.
/// Historical: inventory DONE; HEALTH/RECONNECT opened; seed W7-251 DONE; NEXT advanced to W7-252.
/// </summary>
public sealed class ProductTrancheSeedW7249LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan29AfterPlan28Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));

        Assert.Contains("Intentional residual (W7-249 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-28 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-29", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-250", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-251", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-HEALTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-RECONNECT-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-249 | [#904](https://github.com/sesquicadaver/MTDirector/issues/904) | Seed next after DESK-A11Y-CTRL-01 (PLAN-28 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-250 | [#907](https://github.com/sesquicadaver/MTDirector/issues/907) | PLAN-29 — Inventory Desktop connection health / reconnect after Controller stop (AUDIT §18 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-251 | [#908](https://github.com/sesquicadaver/MTDirector/issues/908) | Seed first PLAN-29 atomic row after inventory → DESK-CONN-HEALTH-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-28 COMPLETE", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-249 DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", plan28, StringComparison.Ordinal);

        Assert.Contains("PLAN-29", plan, StringComparison.Ordinal);
        Assert.Contains("W7-250", plan, StringComparison.Ordinal);
        Assert.Contains("W7-249 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-CONN-HEALTH-01", plan29, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-RECONNECT-01", plan29, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan29, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", plan29, StringComparison.Ordinal);
        Assert.Contains("RunReconnectLoopAsync", plan29, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-250**", limitations, StringComparison.Ordinal);
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
