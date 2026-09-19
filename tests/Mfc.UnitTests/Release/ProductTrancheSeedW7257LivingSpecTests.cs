using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-257: known-limitations / queue seed locked WATCH-OWN-01 (W7-258) after PLAN-30 inventory.
/// Historical: OWN-01 DONE; seed W7-259 DONE; NEXT advanced to W7-260 WATCH-BP-01.
/// </summary>
public sealed class ProductTrancheSeedW7257LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedWatchOwn01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));

        Assert.Contains("Intentional residual (W7-257 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-258", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-259", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-258**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-257 | [#920](https://github.com/sesquicadaver/MTDirector/issues/920) | Seed first PLAN-30 atomic row after inventory → WATCH-OWN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-258 | [#922](https://github.com/sesquicadaver/MTDirector/issues/922) | WATCH-OWN-01 — Bind Watch RPCs to operation owner beyond Read permission | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-259 | [#923](https://github.com/sesquicadaver/MTDirector/issues/923) | Seed next PLAN-30 row after WATCH-OWN-01 → WATCH-BP-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-260 | [#927](https://github.com/sesquicadaver/MTDirector/issues/927) | WATCH-BP-01 — Bounded ProgressHub subscriber channels / slow-subscriber backpressure + live `_history` cap | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-391 (#1187)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-257 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-258", plan, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-391 (#1187)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-257 (#920) DONE", plan30, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-258", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-391 (#1187)", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-259", plan30, StringComparison.Ordinal);
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
