using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-239: known-limitations / queue seed locked DESK-A11Y-SNAP-01 (W7-240) after PLAN-27 inventory. Historical: SNAP-01 DONE; seed W7-241 DONE; NEXT advanced to W7-243.
/// </summary>
public sealed class ProductTrancheSeedW7239LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11ySnap01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));

        Assert.Contains("Intentional residual (W7-239 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-240", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-241", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-239 | [#884](https://github.com/sesquicadaver/MTDirector/issues/884) | Seed first PLAN-27 atomic row after inventory → DESK-A11Y-SNAP-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-240 | [#886](https://github.com/sesquicadaver/MTDirector/issues/886) | DESK-A11Y-SNAP-01 — Snapshot Capture/Reload/Compare/Copy AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-373 (#1152)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-239 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-240", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-373 (#1152)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-239 DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-240", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-373 (#1152)", plan27, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-240**", limitations, StringComparison.Ordinal);
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
