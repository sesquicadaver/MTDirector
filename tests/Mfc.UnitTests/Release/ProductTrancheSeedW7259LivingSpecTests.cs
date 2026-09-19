using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-259: seed locked WATCH-BP-01 (W7-260); opens PLAN-30 COMPLETE follow-up (W7-261).
/// Historical: BP-01 DONE; W7-261 DONE; NEXT advanced to W7-262 PLAN-31 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7259LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedWatchBp01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));

        Assert.Contains("Intentional residual (W7-259 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-260", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-261", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-260**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-259 | [#923](https://github.com/sesquicadaver/MTDirector/issues/923) | Seed next PLAN-30 row after WATCH-OWN-01 → WATCH-BP-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-260 | [#927](https://github.com/sesquicadaver/MTDirector/issues/927) | WATCH-BP-01 — Bounded ProgressHub subscriber channels / slow-subscriber backpressure + live `_history` cap | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-261 | [#928](https://github.com/sesquicadaver/MTDirector/issues/928) | Seed next after WATCH-BP-01 (PLAN-30 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-387 (#1179)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-259 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-260", plan, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-261", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-387 (#1179)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-259 (#923) DONE", plan30, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-260 (#927)", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-261 (#928)", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-387 (#1179)", plan30, StringComparison.Ordinal);
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
